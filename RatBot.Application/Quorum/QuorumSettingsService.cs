namespace RatBot.Application.Quorum;

public sealed class QuorumSettingsService(IQuorumSettingsRepository repository, ILogger logger)
{
    private readonly ILogger _logger = logger.ForContext<QuorumSettingsService>();

    public async Task<ErrorOr<QuorumSettingsUpsertResult>> UpsertAsync(
        ulong guildId,
        QuorumSettingsType targetType,
        ulong targetId,
        IEnumerable<ulong> roleIds,
        double quorumProportion,
        CancellationToken ct = default)
    {
        ErrorOr<QuorumSettings> existingResult = await repository.GetAsync(guildId, targetType, targetId);
        bool created = existingResult.IsError;

        QuorumSettings config = new QuorumSettings(
            guildId,
            targetType,
            targetId,
            roleIds.Distinct().ToArray(),
            quorumProportion);

        ErrorOr<Success> upsertResult = await repository.UpsertAsync(config);

        if (upsertResult.IsError)
            return upsertResult.Errors;

        _logger.Information(
            "Quorum settings {Action} for guild {GuildId}, target type {TargetType}, target {TargetId}.",
            created
                ? "created"
                : "updated",
            guildId,
            targetType,
            targetId);

        return new QuorumSettingsUpsertResult(created, config);
    }

    public async Task<ErrorOr<Deleted>> DeleteAsync(
        ulong guildId,
        QuorumSettingsType targetType,
        ulong targetId,
        CancellationToken ct = default)
    {
        ErrorOr<Deleted> result = await repository.DeleteAsync(guildId, targetType, targetId);

        _logger.Information(
            "Quorum settings delete attempted for guild {GuildId}, target type {TargetType}, target {TargetId}. Success={IsSuccess}",
            guildId,
            targetType,
            targetId,
            !result.IsError);

        return result;
    }

    public async Task<ErrorOr<QuorumSettings>> GetEffectiveAsync(
        ulong guildId,
        ulong channelId,
        ulong? categoryId,
        CancellationToken ct = default)
    {
        ErrorOr<QuorumSettings> channelConfig = await repository.GetAsync(
            guildId,
            QuorumSettingsType.Channel,
            channelId);

        if (!channelConfig.IsError)
            return channelConfig;

        return categoryId is null
            ? channelConfig
            : await repository.GetAsync(guildId, QuorumSettingsType.Category, categoryId.Value);
    }
}