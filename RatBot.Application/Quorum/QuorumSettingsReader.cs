namespace RatBot.Application.Quorum;

public sealed class QuorumSettingsReader(IQuorumSettingsRepository repository) : IQuorumSettingsReader
{
    public Task<ErrorOr<QuorumSettings>> GetAsync(
        QuorumTarget target,
        CancellationToken ct = default)
    {
        _ = ct;
        return repository.GetAsync(target);
    }

    public async Task<ErrorOr<QuorumSettings>> GetEffectiveAsync(
        ulong guildId,
        ulong channelId,
        ulong? categoryId,
        CancellationToken ct = default)
    {
        ErrorOr<QuorumTarget> channelTarget = QuorumTarget.Create(guildId, QuorumSettingsType.Channel, channelId);

        if (channelTarget.IsError)
            return channelTarget.Errors;

        ErrorOr<QuorumSettings> channelConfig = await repository.GetAsync(channelTarget.Value);

        if (!channelConfig.IsError || channelConfig.Errors.Any(error => error.Type != ErrorType.NotFound))
            return channelConfig;

        if (categoryId is null)
            return channelConfig;

        ErrorOr<QuorumTarget> categoryTarget = QuorumTarget.Create(
            guildId,
            QuorumSettingsType.Category,
            categoryId.Value);

        return categoryTarget.IsError
            ? categoryTarget.Errors
            : await repository.GetAsync(categoryTarget.Value);
    }
}
