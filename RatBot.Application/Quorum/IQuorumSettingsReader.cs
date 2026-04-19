namespace RatBot.Application.Quorum;

public interface IQuorumSettingsReader
{
    Task<ErrorOr<QuorumSettings>> GetAsync(
        QuorumTarget target,
        CancellationToken ct = default);

    Task<ErrorOr<QuorumSettings>> GetEffectiveAsync(
        ulong guildId,
        ulong channelId,
        ulong? categoryId,
        CancellationToken ct = default);
}