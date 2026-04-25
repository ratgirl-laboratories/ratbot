using RatBot.Infrastructure.Data;

namespace RatBot.Infrastructure.RoleColours;

public static class DisableRoleColourOption
{

    public async static Task<ErrorOr<RoleColourOption>> ExecuteAsync(
        BotDbContext db,
        Command command,
        CancellationToken ct)
    {
        string key = command.Key.Trim();
        if (string.IsNullOrWhiteSpace(key))
            return Error.Validation(description: "Key is required.");

        string normalized = key.ToUpperInvariant();

        RoleColourOption? option = await db
            .RoleColourOptions
            .SingleOrDefaultAsync(o => o.NormalisedKey == normalized, ct);

        if (option is null)
            return Error.NotFound(description: $"Colour option `{key}` is not registered.");

        if (!option.IsEnabled)
            return Error.Validation(description: $"Colour option `{key}` is already disabled.");

        option.Disable();
        await db.SaveChangesAsync(ct);
        return option;
    }
    public sealed record Command(string Key);
}
