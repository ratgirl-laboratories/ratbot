namespace RatBot.Domain.Features.Quorum;

public readonly record struct QuorumCalculation(int EligibleVoterCount, QuorumProportion Proportion, int RequiredVotes);
