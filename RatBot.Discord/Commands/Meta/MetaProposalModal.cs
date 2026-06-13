namespace RatBot.Discord.Commands.Meta;

public record MetaProposalModal : IModal
{
    [InputLabel("Title")]
    [ModalTextInput(
        "title",
        maxLength: MetaProposalState.MaxTitleLength,
        placeholder: "The title of the proposal thread")]
    public required string ProposalTitle { get; [UsedImplicitly] init; }

    [InputLabel("Summary")]
    [ModalTextInput(
        "summary",
        TextInputStyle.Paragraph,
        maxLength: 1500,
        placeholder: "Please provide a brief, high-level overview of your proposal. (1500 characters)"
    )]
    public required string Summary { get; [UsedImplicitly] init; }

    [InputLabel("Motivation")]
    [ModalTextInput(
        "motivation",
        TextInputStyle.Paragraph,
        maxLength: 1950,
        placeholder:
        "Please provide a detailed explanation of what your proposal seeks to address. (1950 characters)"
    )]
    public required string Motivation { get; [UsedImplicitly] init; }

    [InputLabel("Specification")]
    [ModalTextInput(
        "specification",
        TextInputStyle.Paragraph,
        maxLength: 1950,
        placeholder:
        "Please provide a concrete description of the proposed change or policy. (1950 characters)"
    )]
    public required string Specification { get; [UsedImplicitly] init; }

    string IModal.Title => "Make a proposal";
}