namespace RatBot.Discord.Commands.Meta;

public record MetaVetoModal : IModal
{
    [InputLabel("Reason")]
    [ModalTextInput("reason", TextInputStyle.Paragraph, maxLength: 1950, placeholder: "Reason for veto")]
    public required string Reason { get; [UsedImplicitly] init; }

    string IModal.Title => "Veto proposal";
}
