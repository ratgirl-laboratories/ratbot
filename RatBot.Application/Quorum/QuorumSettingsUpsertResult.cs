namespace RatBot.Application.Quorum;

public sealed record QuorumSettingsUpsertResult(bool Created, QuorumSettings Config);