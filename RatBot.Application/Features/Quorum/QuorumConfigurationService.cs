using RatBot.Domain.Enums;
using RatBot.Domain.Features.Quorum;

namespace RatBot.Application.Features.Quorum;

public sealed class QuorumConfigurationService(IQuorumConfigurationRepository repository, ILogger logger)
{
    private readonly ILogger _logger = logger.ForContext<QuorumConfigurationService>();

    public async Task<(bool Created, QuorumConfig Config)> UpsertAsync(
        ulong guildId,
        QuorumConfigType targetType,
        ulong targetId,
        IReadOnlyCollection<ulong> roleIds,
        double quorumProportion,
        CancellationToken ct = default)
    {
        ArgumentOutOfRangeException.ThrowIfZero(guildId);
        ArgumentOutOfRangeException.ThrowIfZero(targetId);
        ArgumentNullException.ThrowIfNull(roleIds);

        QuorumConfig config = new QuorumConfig(
            guildId,
            targetType,
            targetId,
            roleIds.Distinct().ToArray(),
            quorumProportion);
        bool created = await repository.UpsertAsync(config, ct);

        _logger.Information(
            "Quorum configuration {Action} for guild {GuildId}, target type {TargetType}, target {TargetId}.",
            created ? "created" : "updated",
            guildId,
            targetType,
            targetId);

        return (created, config);
    }

    public async Task<bool> DeleteAsync(
        ulong guildId,
        QuorumConfigType targetType,
        ulong targetId,
        CancellationToken ct = default)
    {
        bool deleted = await repository.DeleteAsync(guildId, targetType, targetId, ct);

        _logger.Information(
            "Quorum configuration delete attempted for guild {GuildId}, target type {TargetType}, target {TargetId}. Deleted={Deleted}",
            guildId,
            targetType,
            targetId,
            deleted);

        return deleted;
    }

    public async Task<QuorumConfig?> GetEffectiveAsync(
        ulong guildId,
        ulong channelId,
        ulong? categoryId,
        CancellationToken ct = default)
    {
        QuorumConfig? channelConfig = await repository.GetAsync(guildId, QuorumConfigType.Channel, channelId, ct);
        if (channelConfig is not null)
            return channelConfig;

        return categoryId is null
            ? null
            : await repository.GetAsync(guildId, QuorumConfigType.Category, categoryId.Value, ct);
    }
}
