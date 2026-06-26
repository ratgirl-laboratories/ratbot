namespace RatBot.Discord.Features.Meta.Commands;

internal static class MetaCommandIds
{
    public const string ProposalModalPrefix = "meta-propose";
    public const string VetoModalPrefix = "meta-veto";
    public const string ResubmitPrefix = "meta-resubmit";
    public const uint DefaultPollHours = 72;

    public static string ResubmitCustomId(Guid stateId) => $"{ResubmitPrefix}:{stateId:N}";
}
