namespace RatBot.Application.Rps;

public static class RpsErrors
{
    public static readonly Error GameNotFound = Error.NotFound(
        "Rps.GameNotFound",
        "That game is no longer active.");

    public static readonly Error UnauthorizedUser = Error.Forbidden(
        "Rps.UnauthorizedUser",
        "You are not part of this game.");

    public static readonly Error SameUser = Error.Validation(
        "Rps.SameUser",
        "Challenger and opponent must be different users.");
}