using RatBot.Domain.Moderation;

namespace RatBot.Application.Moderation;

public sealed class ImageSpamSettingsService(
    IImageSpamSettingsStore store,
    ImageBurstSpamDetectorSettings detectorSettings)
{
    public async Task<ImageBurstSpamDetectorOptions> GetCurrentAsync(CancellationToken ct)
    {
        ImageSpamSettings? settings = await store.GetAsync(ct).ConfigureAwait(false);

        if (settings is null)
            return detectorSettings.Current;

        ImageBurstSpamDetectorOptions options = ToOptions(settings);
        detectorSettings.Update(options);

        return options;
    }

    public async Task<ImageBurstSpamDetectorOptions> UpsertAsync(
        int? requiredChannelCount,
        int? requiredAttachedMessageCount,
        int? burstDurationSeconds,
        CancellationToken ct)
    {
        ImageSpamSettings settings = await store
            .UpsertAsync(requiredChannelCount, requiredAttachedMessageCount, burstDurationSeconds, ct)
            .ConfigureAwait(false);

        ImageBurstSpamDetectorOptions options = ToOptions(settings);
        detectorSettings.Update(options);

        return options;
    }

    private static ImageBurstSpamDetectorOptions ToOptions(ImageSpamSettings settings) =>
        new ImageBurstSpamDetectorOptions
        {
            Window = settings.BurstDurationSeconds,
            DistinctChannelThreshold = settings.RequiredChannelCount,
            RequiredAttachedMessageCount = settings.RequiredAttachedMessageCount,
        };
}
