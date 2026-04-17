namespace RatBot.Application.Moderation;

public static class ModerationErrors
{
    public static Error UserAlreadyAutobanned(ulong userId) =>
        Error.Conflict(
            "Moderation.UserAlreadyAutobanned",
            $"User {userId} is already registered for autoban.");
}