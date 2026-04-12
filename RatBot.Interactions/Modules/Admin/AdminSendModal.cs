namespace RatBot.Interactions.Modules.Admin;

[UsedImplicitly]
public record AdminSendModal : IModal
{
    public string Title => "Send message as ratbot to a channel";

    [InputLabel("Message")]
    [ModalTextInput("message", TextInputStyle.Paragraph, "Message to send as the bot.", maxLength: 4000)]
    public required string Message { get; init; }
}