namespace RatBot.Domain.Features.Quorum;

public static class QuorumCalculator
{
    public static ErrorOr<QuorumCalculation> Calculate(int eligibleVoterCount, QuorumProportion proportion)
    {
        if (eligibleVoterCount <= 0)
            return Error.Validation(description: "No eligible voters were found.");

        int requiredVotes = (int)Math.Ceiling(eligibleVoterCount * proportion.Value);

        return new QuorumCalculation(eligibleVoterCount, proportion, requiredVotes);
    }
}
