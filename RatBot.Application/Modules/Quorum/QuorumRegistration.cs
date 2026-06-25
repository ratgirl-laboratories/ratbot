using RatBot.Domain.Modules.Quorum;

namespace RatBot.Application.Modules.Quorum;

public readonly record struct QuorumRegistration(bool Created, QuorumConfiguration Configuration);
