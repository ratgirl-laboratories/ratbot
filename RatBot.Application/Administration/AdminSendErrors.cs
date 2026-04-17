namespace RatBot.Application.Administration;

public static class AdminSendErrors
{
    public static readonly Error ChannelNotFound = Error.NotFound(
        "AdminSend.ChannelNotFound",
        "I couldn't find that channel. Run `/admin send` again.");

    public static readonly Error InsufficientPermissions = Error.Forbidden(
        "AdminSend.InsufficientPermissions",
        "I don't have permission to post in that channel.");

    public static readonly Error EmptyMessage = Error.Validation(
        "AdminSend.EmptyMessage",
        "Message cannot be empty.");
}