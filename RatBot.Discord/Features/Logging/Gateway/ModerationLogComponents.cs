using System.Collections.Immutable;
using System.Globalization;
using RatBot.Application.Features.Logging;

namespace RatBot.Discord.Features.Logging.Gateway;

internal static class ModerationLogComponents
{
    public const string TimestampFormat = "yyyy-MM-dd HH:mm:ss'Z'";
    private const int EditColor = 14399750;

    public static ModerationLogMessage BuildEditLog(
        string messageJumpUrl,
        ulong channelId,
        ulong authorId,
        string? beforeContent,
        string? afterContent,
        IReadOnlyCollection<CachedAttachmentEvidence> attachments
    )
    {
        ImmutableArray<ModerationLogAttachment> attachmentModels = BuildAttachmentModels(attachments);
        Embed embed = new EmbedBuilder()
            .WithDescription(EditContentBlock(beforeContent, afterContent))
            .WithColor(new Color(EditColor))
            .AddField("Author", $"<@{authorId}> (`{authorId}`)", true)
            .AddField("Message", $"[Link]({messageJumpUrl})", true)
            .AddField("Channel", $"<#{channelId}>", true)
            .Build();

        return new ModerationLogMessage(embed, attachmentModels);
    }

    public static ModerationLogMessage BuildDeleteLog(
        ulong channelId,
        ulong authorId,
        string? precedingMessageJumpUrl,
        string? content,
        IReadOnlyCollection<CachedAttachmentEvidence> attachments
    )
    {
        ImmutableArray<ModerationLogAttachment> attachmentModels = BuildAttachmentModels(attachments);
        Embed embed = new EmbedBuilder()
            .WithDescription(ContentBlock(content))
            .WithColor(new Color(0xFF0000))
            .AddField("Author", $"<@{authorId}> (`{authorId}`)", true)
            .AddField("Message", BuildDeleteMessageFieldValue(precedingMessageJumpUrl), true)
            .AddField("Channel", $"<#{channelId}>", true)
            .Build();

        return new ModerationLogMessage(embed, attachmentModels);
    }

    public static string FormatTimestamp(DateTimeOffset timestamp) =>
        timestamp.ToUniversalTime().ToString(TimestampFormat, CultureInfo.InvariantCulture);

    public static string GetJumpUrl(IMessage message) => message.GetJumpUrl();

    private static string BuildDeleteMessageFieldValue(string? precedingMessageJumpUrl) =>
        string.IsNullOrWhiteSpace(precedingMessageJumpUrl) ? "Unavailable" : $"[Link]({precedingMessageJumpUrl})";

    private static ImmutableArray<ModerationLogAttachment> BuildAttachmentModels(IReadOnlyCollection<CachedAttachmentEvidence> attachments) =>
        attachments.Select(BuildAttachmentModel).ToImmutableArray();

    private static ModerationLogAttachment BuildAttachmentModel(CachedAttachmentEvidence attachment)
    {
        string fileName = $"evidence-{attachment.Index}{GetExtension(attachment.ContentType)}";
        return new ModerationLogAttachment(fileName, attachment.ContentType, attachment.Bytes);
    }

    private static string GetExtension(string contentType) =>
        contentType.ToLowerInvariant() switch
        {
            "image/jpeg" => ".jpg",
            "image/png" => ".png",
            "image/gif" => ".gif",
            "image/webp" => ".webp",
            "video/mp4" => ".mp4",
            "video/webm" => ".webm",
            "video/quicktime" => ".mov",
            _ => ".bin",
        };

    private static string ContentBlock(string? value) => value is null ? "*Unavailable*" : CodeBlock(value);

    private static string EditContentBlock(string? beforeContent, string? afterContent)
    {
        if (beforeContent is null || afterContent is null)
            return $"{AnsiCodeBlock(beforeContent ?? "*Unavailable*")}\n{AnsiCodeBlock(afterContent ?? "*Unavailable*")}";

        MessageEditDiff diff = new MessageEditDiffer().BuildDiff(beforeContent, afterContent);
        return $"{AnsiCodeBlock(MessageEditDiffAnsiRenderer.RenderBefore(diff))}\n{AnsiCodeBlock(MessageEditDiffAnsiRenderer.RenderAfter(diff))}";
    }

    private static string CodeBlock(string value)
    {
        string sanitized = value.Replace("```", "`\u200b``", StringComparison.Ordinal);
        return $"```{sanitized}```";
    }

    private static string AnsiCodeBlock(string value)
    {
        string sanitized = value.Replace("```", "`\u200b``", StringComparison.Ordinal);
        return $"```ansi\n{sanitized}\n```";
    }

    internal sealed record ModerationLogMessage(Embed Embed, ImmutableArray<ModerationLogAttachment> Attachments);

    internal sealed record ModerationLogAttachment(string FileName, string ContentType, byte[] Bytes)
    {
        public string AttachmentUrl => $"attachment://{FileName}";
    }
}
