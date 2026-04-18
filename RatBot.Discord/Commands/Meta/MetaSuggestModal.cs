namespace RatBot.Discord.Commands.Meta;

[UsedImplicitly]
public record MetaSuggestModal : IModal
{
    [InputLabel("Title")]
    [ModalTextInput("title", maxLength: 75, placeholder: "The title of your suggestion")]
    public required string SuggestionTitle { get; [UsedImplicitly] init; }

    [InputLabel("Summary")]
    [ModalTextInput(
        "summary",
        TextInputStyle.Paragraph,
        maxLength: 1000,
        placeholder: "Please provide a brief, high-level overview of your suggestion. (1000 characters)"
    )]
    public required string Summary { get; [UsedImplicitly] init; }

    [InputLabel("Motivation")]
    [ModalTextInput(
        "motivation",
        TextInputStyle.Paragraph,
        maxLength: 1950,
        placeholder:
        "Please provide a detailed explanation of what your suggestion seeks to address and how it will benefit the community. (1950 characters)"
    )]
    public required string Motivation { get; [UsedImplicitly] init; }

    [InputLabel("Specification")]
    [ModalTextInput(
        "specification",
        TextInputStyle.Paragraph,
        maxLength: 1950,
        placeholder:
        "A detailed description of the proposal and what changes need to be made. This should include a description of how the proposed features should be used, who the audience for the new feature is, and which decisions (if any) should be made by the committee. (1950 characters)"
    )]
    public required string Specification { get; [UsedImplicitly] init; }

    string IModal.Title => "Make a suggestion";
}