using System.Text;
using System.Text.RegularExpressions;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using RatBot.Domain.Entities;
using RatBot.Infrastructure.Services;

namespace RatBot.Interactions;

[Group("virtue-mod", "Virtue moderation commands.")]
[DefaultMemberPermissions(GuildPermission.BanMembers)]
public sealed partial class VirtueModModule(
    UserVirtueService userVirtueService,
    EmojiVirtueService emojiVirtueService,
    DiscordSocketClient discordClient
) : SlashCommandBase
{
    private const int MaxMappedEmoji = 30;

    [SlashCommand("user-virtue", "Get a user's tracked virtue.")]
    [RequireUserPermission(GuildPermission.BanMembers)]
    public async Task UserVirtueAsync(IUser user)
    {
        if (!await TryDeferEphemeralAsync())
            return;

        int? virtue = await userVirtueService.TryGetVirtueAsync(user.Id);
        if (virtue is null)
        {
            await SendEphemeralAsync($"{user.Mention} does not have a recorded virtue yet.");
            return;
        }

        await SendEphemeralAsync($"{user.Mention} has a virtue of `{virtue.Value}`.");
    }

    [SlashCommand("add-emoji", "Add or update an emoji virtue mapping.")]
    [RequireUserPermission(GuildPermission.BanMembers)]
    public async Task AddEmojiAsync(string emoji, int virtue)
    {
        if (virtue is < -15 or > 15)
        {
            await RespondAsync("Virtue must be between -15 and 15.", ephemeral: true);
            return;
        }

        string emojiId = ResolveEmojiId(emoji);
        if (string.IsNullOrWhiteSpace(emojiId))
        {
            await RespondAsync(
                "Invalid emoji input. Use a unicode emoji, custom emoji mention (`<:name:id>`), or emoji ID.",
                ephemeral: true
            );
            return;
        }

        int? existingVirtue = await emojiVirtueService.GetVirtueAsync(emojiId);
        if (existingVirtue is null)
        {
            int configuredCount = await emojiVirtueService.CountAsync();
            if (configuredCount >= MaxMappedEmoji)
            {
                await RespondAsync(
                    $"You already have `{MaxMappedEmoji}` scored emojis configured. Remove one before adding another.",
                    ephemeral: true
                );
                return;
            }
        }

        await emojiVirtueService.UpsertVirtueAsync(emojiId, virtue);
        string displayEmoji = FormatEmojiForDisplay(emojiId);

        await RespondAsync($"Saved virtue mapping: {displayEmoji} => `{virtue}`.", ephemeral: true);
    }

    [SlashCommand("list-emojis", "List all configured emoji virtue mappings.")]
    [RequireUserPermission(GuildPermission.BanMembers)]
    public async Task ListEmojisAsync()
    {
        if (!await TryDeferEphemeralAsync())
            return;

        List<EmojiVirtue> virtues = await emojiVirtueService.ListVirtuesAsync();
        if (virtues.Count == 0)
        {
            await SendEphemeralAsync("No emoji virtue mappings are configured yet.");
            return;
        }

        StringBuilder text = new StringBuilder("Configured emoji virtues:\n");

        foreach (EmojiVirtue row in virtues)
        {
            string displayEmoji = FormatEmojiForDisplay(row.EmojiId);
            text.AppendLine($"{displayEmoji}: {row.Virtue}");
        }

        await SendEphemeralAsync(text.ToString());
    }

    [SlashCommand("remove-emoji", "Remove an emoji virtue mapping.")]
    [RequireUserPermission(GuildPermission.BanMembers)]
    public async Task RemoveEmojiAsync(string emoji)
    {
        string emojiId = ResolveEmojiId(emoji);
        if (string.IsNullOrWhiteSpace(emojiId))
        {
            await RespondAsync(
                "Invalid emoji input. Use a unicode emoji, custom emoji mention (`<:name:id>`), or emoji ID.",
                ephemeral: true
            );
            return;
        }

        bool removed = await emojiVirtueService.RemoveEmojiAsync(emojiId);
        if (!removed)
        {
            await RespondAsync("That emoji is not currently mapped to a virtue score.", ephemeral: true);
            return;
        }

        string displayEmoji = FormatEmojiForDisplay(emojiId);
        await RespondAsync($"Removed virtue mapping for {displayEmoji}.", ephemeral: true);
    }

    private string FormatEmojiForDisplay(string emojiId)
    {
        if (string.IsNullOrWhiteSpace(emojiId))
            return "[unknown]";

        if (!ulong.TryParse(emojiId, out ulong parsedEmojiId))
            return emojiId;

        foreach (SocketGuild guild in discordClient.Guilds)
        {
            GuildEmote? guildEmote = guild.Emotes.FirstOrDefault(x => x.Id == parsedEmojiId);
            if (guildEmote is not null)
                return guildEmote.ToString();
        }

        return $"[custom:{parsedEmojiId}]";
    }

    private static string ResolveEmojiId(string input)
    {
        string trimmed = input.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
            return string.Empty;

        if (ulong.TryParse(trimmed, out _))
            return trimmed;

        Match customMatch = GetEmojiRegex().Match(trimmed);
        return customMatch.Success ? customMatch.Groups[1].Value : trimmed;
    }

    [GeneratedRegex("^<a?:[^:]+:(\\d+)>$")]
    private static partial Regex GetEmojiRegex();
}
