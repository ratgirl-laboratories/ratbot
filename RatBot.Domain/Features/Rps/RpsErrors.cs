using ErrorOr;

namespace RatBot.Domain.Features.Rps;

public static class RpsErrors
{
    public static readonly Error GameNotFound = Error.NotFound(
        code: "Rps.GameNotFound",
        description: "That game is no longer active.");

    public static readonly Error UnauthorizedUser = Error.Forbidden(
        code: "Rps.UnauthorizedUser",
        description: "You are not part of this game.");

    public static readonly Error SameUser = Error.Validation(
        code: "Rps.SameUser",
        description: "Challenger and opponent must be different users.");
}