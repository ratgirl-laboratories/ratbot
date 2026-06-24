namespace RatBot.Application.Meta;

public static class MetaProposalErrors
{
    public static readonly Error SettingsNotConfigured = Error.NotFound(
        "MetaProposal.SettingsNotConfigured",
        "Meta proposal workflow settings are not fully configured."
    );

    public static readonly Error SuggestionNotTracked = Error.NotFound(
        "MetaProposal.SuggestionNotTracked",
        "This channel is not a tracked suggestion thread."
    );

    public static readonly Error ThreadNotTracked = Error.NotFound(
        "MetaProposal.ThreadNotTracked",
        "This thread is not tracked by the meta proposal workflow."
    );

    public static readonly Error Unauthorized = Error.Forbidden(
        "MetaProposal.Unauthorized",
        "You are not allowed to use this meta proposal action here."
    );

    public static readonly Error RetryCooldownActive = Error.Conflict(
        "MetaProposal.RetryCooldownActive",
        "That proposal publication retry is still on cooldown."
    );
}
