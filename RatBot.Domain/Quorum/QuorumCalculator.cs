namespace RatBot.Domain.Quorum;

public static class QuorumCalculator
{
    public static int CalculateRequiredMemberCount(int eligibleMemberCount, double quorumProportion)
    {
        if (double.IsNaN(quorumProportion) || double.IsInfinity(quorumProportion) || quorumProportion is <= 0 or > 1)
            throw new ArgumentOutOfRangeException(nameof(quorumProportion));

        return (int)Math.Floor(eligibleMemberCount * quorumProportion);
    }
}