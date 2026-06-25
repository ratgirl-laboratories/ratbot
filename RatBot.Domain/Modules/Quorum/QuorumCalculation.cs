namespace RatBot.Domain.Modules.Quorum;

public readonly record struct QuorumCalculation(int EligibleVoterCount, QuorumProportion Proportion, int RequiredVotes);
