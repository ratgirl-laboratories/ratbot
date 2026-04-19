namespace RatBot.Application.Quorum;

public interface IQuorumSettingsWriter
{
    Task<ErrorOr<QuorumSettingsUpsertResult>> UpsertAsync(
        QuorumTarget target,
        IEnumerable<ulong> roleIds,
        double quorumProportion,
        CancellationToken ct = default);

    Task<ErrorOr<Deleted>> DeleteAsync(
        QuorumTarget target,
        CancellationToken ct = default);
}