using System.Collections.Immutable;
using System.Globalization;
using RatBot.Application.Features.Logging;

namespace RatBot.Discord.Features.Logging.Gateway;

internal static class ModerationLogComponents
{
    public const string TimestampFormat = "yyyy-MM-dd HH:mm:ss'Z'";

    public static ModerationLogMessage BuildEditLog(
        string messageJumpUrl,
        ulong authorId,
        DateTimeOffset editedAt,
        string? beforeContent,
        string? afterContent,
        IReadOnlyCollection<CachedAttachmentEvidence> attachments
    )
    {
        ImmutableArray<ModerationLogAttachment> attachmentModels = BuildAttachmentModels(attachments);
        ContainerBuilder container = new ContainerBuilder()
            .WithAccentColor(Color.Orange)
            .WithTextDisplay(new TextDisplayBuilder().WithContent("## Message Edit"))
            .WithTextDisplay(
                new TextDisplayBuilder().WithContent(
                    $"**Message**: [Jump to message]({messageJumpUrl})\n"
                        + $"**Author**: <@{authorId}> (`{authorId}`)\n"
                        + $"**Edited**: {FormatTimestamp(editedAt)}"
                )
            )
            .WithSeparator(new SeparatorBuilder())
            .WithTextDisplay(new TextDisplayBuilder().WithContent("### Content"))
            .WithTextDisplay(new TextDisplayBuilder().WithContent(EditContentBlock(beforeContent, afterContent)));

        AddAttachmentComponents(container, attachmentModels);

        return new ModerationLogMessage(new ComponentBuilderV2(container).Build(), attachmentModels);
    }

    public static ModerationLogMessage BuildDeleteLog(
        ulong channelId,
        ulong authorId,
        DateTimeOffset? observedAt,
        DateTimeOffset deletedAt,
        string? precedingMessageJumpUrl,
        string? content,
        IReadOnlyCollection<CachedAttachmentEvidence> attachments
    )
    {
        ImmutableArray<ModerationLogAttachment> attachmentModels = BuildAttachmentModels(attachments);
        ContainerBuilder container = new ContainerBuilder()
            .WithAccentColor(Color.Red)
            .WithTextDisplay(new TextDisplayBuilder().WithContent("## Message Delete"))
            .WithTextDisplay(new TextDisplayBuilder().WithContent("### Metadata"))
            .WithTextDisplay(
                new TextDisplayBuilder().WithContent(BuildDeleteMetadata(channelId, authorId, observedAt, deletedAt, precedingMessageJumpUrl))
            )
            .WithSeparator(new SeparatorBuilder())
            .WithTextDisplay(new TextDisplayBuilder().WithContent("### Content"))
            .WithTextDisplay(new TextDisplayBuilder().WithContent(ContentBlock(content)));

        AddAttachmentComponents(container, attachmentModels);

        return new ModerationLogMessage(new ComponentBuilderV2(container).Build(), attachmentModels);
    }

    public static string FormatTimestamp(DateTimeOffset timestamp) =>
        timestamp.ToUniversalTime().ToString(TimestampFormat, CultureInfo.InvariantCulture);

    public static string GetJumpUrl(IMessage message) => message.GetJumpUrl();

    private static string BuildDeleteMetadata(
        ulong channelId,
        ulong authorId,
        DateTimeOffset? observedAt,
        DateTimeOffset deletedAt,
        string? precedingMessageJumpUrl
    )
    {
        List<string> lines = new List<string> { $"**Channel**: <#{channelId}>", $"**Author**: <@{authorId}>" };

        if (observedAt is not null)
            lines.Add($"**Observed**: {FormatTimestamp(observedAt.Value)}");

        lines.Add($"**Deleted**: {FormatTimestamp(deletedAt)}");

        if (!string.IsNullOrWhiteSpace(precedingMessageJumpUrl))
            lines.Add($"**Preceding Message**: [Jump to message]({precedingMessageJumpUrl})");

        return string.Join('\n', lines);
    }

    private static void AddAttachmentComponents(ContainerBuilder container, ImmutableArray<ModerationLogAttachment> attachments)
    {
        if (attachments.Length == 0)
            return;

        container.WithSeparator(new SeparatorBuilder()).WithTextDisplay(new TextDisplayBuilder().WithContent("### Attachments"));

        ImmutableArray<ModerationLogAttachment> galleryAttachments = attachments
            .Where(attachment => attachment.IsGalleryCompatible)
            .ToImmutableArray();
        ImmutableArray<ModerationLogAttachment> fileAttachments = attachments.Where(attachment => !attachment.IsGalleryCompatible).ToImmutableArray();

        if (galleryAttachments.Length > 0)
        {
            MediaGalleryBuilder gallery = new MediaGalleryBuilder();

            foreach (ModerationLogAttachment attachment in galleryAttachments)
                gallery.AddItem(attachment.AttachmentUrl, attachment.FileName, false);

            container.WithMediaGallery(gallery);
        }

        foreach (ModerationLogAttachment attachment in fileAttachments)
            container.WithFile(new FileComponentBuilder(new UnfurledMediaItemProperties(attachment.AttachmentUrl), false));
    }

    private static ImmutableArray<ModerationLogAttachment> BuildAttachmentModels(IReadOnlyCollection<CachedAttachmentEvidence> attachments) =>
        attachments.Select(BuildAttachmentModel).ToImmutableArray();

    private static ModerationLogAttachment BuildAttachmentModel(CachedAttachmentEvidence attachment)
    {
        string fileName = $"evidence-{attachment.Index}{GetExtension(attachment.ContentType)}";
        return new ModerationLogAttachment(fileName, attachment.ContentType, attachment.Bytes, IsGalleryCompatible(attachment.ContentType));
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

    private static bool IsGalleryCompatible(string contentType) =>
        contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase) || contentType.StartsWith("video/", StringComparison.OrdinalIgnoreCase);

    private static string ContentBlock(string? value) => value is null ? "*Unavailable*" : CodeBlock(value);

    private static string EditContentBlock(string? beforeContent, string? afterContent)
    {
        if (beforeContent is null || afterContent is null)
            return AnsiCodeBlock($"Before: {beforeContent ?? "*Unavailable*"}\nAfter:  {afterContent ?? "*Unavailable*"}");

        MessageEditDiff diff = new MessageEditDiffer().BuildDiff(beforeContent, afterContent);
        return AnsiCodeBlock(MessageEditDiffAnsiRenderer.Render(diff));
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

    internal sealed record ModerationLogMessage(MessageComponent Components, ImmutableArray<ModerationLogAttachment> Attachments);

    internal sealed record ModerationLogAttachment(string FileName, string ContentType, byte[] Bytes, bool IsGalleryCompatible)
    {
        public string AttachmentUrl => $"attachment://{FileName}";
    }
}
