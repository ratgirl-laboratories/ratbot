using System.Net;
using Discord.Net;
using RatBot.Application.Features.EmojiYoink;

namespace RatBot.Discord.Features.EmojiYoink;

public sealed class DiscordGuildEmojiImporter(DiscordSocketClient client, HttpClient httpClient) : IGuildEmojiImporter
{
    private const int MaximumEmojiBytes = 256 * 1024;

    public async Task<ErrorOr<CreatedGuildEmoji>> ImportAsync(
        ulong guildId,
        ulong moderatorUserId,
        EmojiName destinationName,
        YoinkEmojiSource source,
        CancellationToken ct
    )
    {
        SocketGuild? guild = client.GetGuild(guildId);

        if (guild is null)
            return EmojiYoinkErrors.GuildUnavailable;

        ErrorOr<MemoryStream> downloadResult = await DownloadAsync(source, ct).ConfigureAwait(false);

        if (downloadResult.IsError)
            return downloadResult.Errors;

        using MemoryStream imageStream = downloadResult.Value;
        using Image image = new Image(imageStream);
        RequestOptions options = new RequestOptions
        {
            CancelToken = ct,
            AuditLogReason = $"Emoji yoink by moderator {moderatorUserId}; source emoji {source.EmojiId}",
        };

        try
        {
            GuildEmote created = await guild.CreateEmoteAsync(destinationName.Value, image, options: options).ConfigureAwait(false);

            return new CreatedGuildEmoji(created.Id, created.Name, created.Animated);
        }
        catch (HttpException exception)
        {
            return MapDiscordFailure(exception);
        }
    }

    private async Task<ErrorOr<MemoryStream>> DownloadAsync(YoinkEmojiSource source, CancellationToken ct)
    {
        string animatedQuery = source.IsAnimated ? "?animated=true" : string.Empty;
        Uri cdnUri = new Uri($"https://cdn.discordapp.com/emojis/{source.EmojiId}.webp{animatedQuery}");
        MemoryStream? imageStream = null;

        try
        {
            using HttpResponseMessage response = await httpClient
                .GetAsync(cdnUri, HttpCompletionOption.ResponseHeadersRead, ct)
                .ConfigureAwait(false);

            if (response.StatusCode == HttpStatusCode.NotFound)
                return EmojiYoinkErrors.SourceUnavailable;

            if (!response.IsSuccessStatusCode)
                return EmojiYoinkErrors.DownloadFailed;

            if (response.Content.Headers.ContentLength > MaximumEmojiBytes)
                return EmojiYoinkErrors.ImageTooLarge;

            await using Stream responseStream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
            imageStream = new MemoryStream(MaximumEmojiBytes);
            byte[] buffer = new byte[81920];

            while (true)
            {
                int bytesRead = await responseStream.ReadAsync(buffer, ct).ConfigureAwait(false);

                if (bytesRead == 0)
                    break;

                if (imageStream.Length + bytesRead > MaximumEmojiBytes)
                    return EmojiYoinkErrors.ImageTooLarge;

                await imageStream.WriteAsync(buffer.AsMemory(0, bytesRead), ct).ConfigureAwait(false);
            }

            imageStream.Position = 0;
            MemoryStream completedStream = imageStream;
            imageStream = null;
            return completedStream;
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return EmojiYoinkErrors.DownloadFailed;
        }
        catch (HttpRequestException)
        {
            return EmojiYoinkErrors.DownloadFailed;
        }
        catch (IOException)
        {
            return EmojiYoinkErrors.DownloadFailed;
        }
        finally
        {
            // it aint needed nor APPROPRIATE here SHUT UP analyzers GRRRFRRRR
            // ReSharper disable once MethodHasAsyncOverload
#pragma warning disable MA0042
            imageStream?.Dispose();
#pragma warning restore MA0042
        }
    }

    private static Error MapDiscordFailure(HttpException exception)
    {
        int? discordCode = exception.DiscordCode is null ? null : (int)exception.DiscordCode;

        if (discordCode == 30008)
            return EmojiYoinkErrors.NoEmojiSlots;

        if (discordCode == 50035 || exception.HttpCode == HttpStatusCode.BadRequest)
            return EmojiYoinkErrors.InvalidUpload;

        if (discordCode == 50013 || exception.HttpCode == HttpStatusCode.Forbidden)
            return EmojiYoinkErrors.BotMissingPermission;

        return EmojiYoinkErrors.ImportFailed;
    }
}
