using RatBot.Application.Features.Logging;

namespace RatBot.Discord.Features.Logging;

public sealed class LoggingOptions
{
    public const string SectionName = "Logging";

    public int MaxCachedMessageCount { get; init; } = 500;
    public int MaxAttachmentCountPerMessage { get; init; } = 4;
    public long MaxAttachmentBytesPerAttachment { get; init; } = 8 * 1024 * 1024;
    public long MaxTotalCachedAttachmentBytes { get; init; } = 64 * 1024 * 1024;

    public EvidenceCacheSettings ToEvidenceCacheSettings() =>
        new EvidenceCacheSettings(
            MaxCachedMessageCount,
            MaxAttachmentCountPerMessage,
            MaxAttachmentBytesPerAttachment,
            MaxTotalCachedAttachmentBytes
        );
}
