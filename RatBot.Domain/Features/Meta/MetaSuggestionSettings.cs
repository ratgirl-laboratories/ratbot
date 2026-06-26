namespace RatBot.Domain.Features.Meta;

public sealed class MetaSuggestionSettings
{
    private MetaSuggestionSettings() { }

    public ulong GuildId { get; private set; }
    public ulong SuggestionsForumChannelId { get; private set; }
    public ulong ProposalsForumChannelId { get; private set; }
    public ulong CabinetRoleId { get; private set; }
    public ulong CabinetChairRoleId { get; private set; }
    public ulong CommitteeRoleId { get; private set; }

    public static MetaSuggestionSettings Create(
        ulong guildId,
        ulong suggestionsForumChannelId = 0,
        ulong proposalsForumChannelId = 0,
        ulong cabinetRoleId = 0,
        ulong cabinetChairRoleId = 0,
        ulong committeeRoleId = 0
    ) =>
        new MetaSuggestionSettings
        {
            GuildId = guildId,
            SuggestionsForumChannelId = suggestionsForumChannelId,
            ProposalsForumChannelId = proposalsForumChannelId,
            CabinetRoleId = cabinetRoleId,
            CabinetChairRoleId = cabinetChairRoleId,
            CommitteeRoleId = committeeRoleId,
        };

    private static ErrorOr<Success> SetId(ulong id, Action<ulong> assign)
    {
        if (id == 0)
            return Required("Id");

        assign(id);
        return Result.Success;
    }

    private static Error Required(string fieldName) =>
        Error.Validation($"MetaSuggestionSettings.{fieldName}Required", $"Meta proposal setting {fieldName} must be configured.");

    public ErrorOr<Success> SetSuggestionsForum(ulong channelId) => SetId(channelId, value => SuggestionsForumChannelId = value);

    public ErrorOr<Success> SetProposalsForum(ulong channelId) => SetId(channelId, value => ProposalsForumChannelId = value);

    public ErrorOr<Success> SetCabinetRole(ulong roleId) => SetId(roleId, value => CabinetRoleId = value);

    public ErrorOr<Success> SetCabinetChairRole(ulong roleId) => SetId(roleId, value => CabinetChairRoleId = value);

    public ErrorOr<Success> SetCommitteeRole(ulong roleId) => SetId(roleId, value => CommitteeRoleId = value);

    public ErrorOr<Success> EnsureProposalWorkflowConfigured()
    {
        if (SuggestionsForumChannelId == 0)
            return Required(nameof(SuggestionsForumChannelId));

        if (ProposalsForumChannelId == 0)
            return Required(nameof(ProposalsForumChannelId));

        if (CabinetRoleId == 0)
            return Required(nameof(CabinetRoleId));

        if (CabinetChairRoleId == 0)
            return Required(nameof(CabinetChairRoleId));

        if (CommitteeRoleId == 0)
            return Required(nameof(CommitteeRoleId));

        return Result.Success;
    }
}
