using ErrorOr;

namespace RatBot.Domain.Features.AdminSend;

public static class AdminSendErrors
{
    public static readonly Error ChannelNotFound = Error.NotFound(
        code: "AdminSend.ChannelNotFound",
        description: "I couldn't find that channel. Run `/admin send` again.");

    public static readonly Error InsufficientPermissions = Error.Forbidden(
        code: "AdminSend.InsufficientPermissions",
        description: "I don't have permission to post in that channel.");

    public static readonly Error EmptyMessage = Error.Validation(
        code: "AdminSend.EmptyMessage",
        description: "Message cannot be empty.");
}
