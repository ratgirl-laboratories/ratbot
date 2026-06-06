using RatBot.Domain.Moderation;

namespace RatBot.Application.Moderation;

public interface IImageSpamSettingsStore
{
    Task<ImageSpamSettings?> GetAsync(CancellationToken ct);

    Task<ImageSpamSettings> UpsertAsync(
        int? requiredChannelCount,
        int? requiredAttachmentCount,
        int? burstDurationSeconds,
        CancellationToken ct);
}
