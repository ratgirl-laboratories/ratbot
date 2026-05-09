using RatBot.Infrastructure.Data;

namespace RatBot.Infrastructure.RoleColours;

public static class RegisterRoleColourOption
{
    public async static Task<ErrorOr<RoleColourOption>> ExecuteAsync(
        BotDbContext db,
        string key,
        string label,
        ulong sourceRoleId,
        ulong displayRoleId,
        CancellationToken ct)
    {
        ErrorOr<RoleColourOption> optionResult = RoleColourOption.Create(
            key,
            label,
            sourceRoleId,
            displayRoleId);

        if (optionResult.IsError)
            return optionResult.Errors;

        RoleColourOption option = optionResult.Value;

        List<RoleColourOption> existing = await db.RoleColourOptions.AsNoTracking().ToListAsync(ct);

        if (existing.Exists(o => string.Equals(o.NormalisedKey, option.NormalisedKey, StringComparison.Ordinal)))
            return Error.Conflict(description: $"Colour option `{option.Key}` is already registered.");

        if (existing.Exists(o => o.SourceRoleId == sourceRoleId))
            return Error.Conflict(description: "Source role is already mapped to a colour option.");

        if (existing.Exists(o => o.DisplayRoleId == displayRoleId))
            return Error.Conflict(description: "Display role is already mapped to a colour option.");

        await db.RoleColourOptions.AddAsync(option, ct);
        await db.SaveChangesAsync(ct);

        return option;
    }
}