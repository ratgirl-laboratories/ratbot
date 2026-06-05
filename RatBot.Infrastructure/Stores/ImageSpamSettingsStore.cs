using RatBot.Application.Moderation;
using RatBot.Infrastructure.Data;

namespace RatBot.Infrastructure.Stores;

public sealed class ImageSpamSettingsStore(BotDbContext dbContext) : IImageSpamSettingsStore
{
    public Task<ImageSpamSettings?> GetAsync(CancellationToken ct) =>
        dbContext.ImageSpamSettings
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == ImageSpamSettings.SingletonId, ct);

    public async Task<ImageSpamSettings> UpsertAsync(
        int? requiredChannelCount,
        int? requiredAttachedMessageCount,
        int? burstDurationSeconds,
        CancellationToken ct)
    {
        ImageSpamSettings? settings = await dbContext.ImageSpamSettings
            .SingleOrDefaultAsync(x => x.Id == ImageSpamSettings.SingletonId, ct)
            .ConfigureAwait(false);

        if (settings is null)
        {
            settings = ImageSpamSettings.CreateDefault();
            dbContext.ImageSpamSettings.Add(settings);
        }

        settings.Update(requiredChannelCount, requiredAttachedMessageCount, burstDurationSeconds);
        await dbContext.SaveChangesAsync(ct).ConfigureAwait(false);

        return settings;
    }
}
