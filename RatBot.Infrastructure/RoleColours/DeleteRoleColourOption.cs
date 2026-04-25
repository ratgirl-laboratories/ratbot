using RatBot.Infrastructure.Data;

namespace RatBot.Infrastructure.RoleColours;

public static class DeleteRoleColourOption
{

    public static async Task<ErrorOr<RoleColourOption>> ExecuteAsync(
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

        // Any members that had this option selected should become NoColour
        List<MemberColourPreference> affected = await db.MemberColourPreferences
            .Where(p => p.Kind == MemberColourPreferenceKind.ConfiguredOption
                        && p.SelectedOptionId != null
                        && p.SelectedOptionId.Value == option.OptionId)
            .ToListAsync(ct);

        foreach (MemberColourPreference pref in affected)
            pref.SelectNoColour();

        db.RoleColourOptions.Remove(option);
        await db.SaveChangesAsync(ct);

        return option;
    }
    public sealed record Command(string Key);
}
