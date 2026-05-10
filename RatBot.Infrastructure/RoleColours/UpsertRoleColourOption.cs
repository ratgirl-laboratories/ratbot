using RatBot.Infrastructure.Data;

namespace RatBot.Infrastructure.RoleColours;

public static class UpsertRoleColourOption
{
    public async static Task<ErrorOr<Result>> ExecuteAsync(
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

        RoleColourOption candidate = optionResult.Value;

        List<RoleColourOption> existing = await db.RoleColourOptions.ToListAsync(ct);
        RoleColourOption? option = existing.SingleOrDefault(o => o.SourceRoleId == sourceRoleId);

        bool isKeyConflict = existing.Exists(o =>
            string.Equals(o.NormalisedKey, candidate.NormalisedKey, StringComparison.Ordinal)
            && o.OptionId != option?.OptionId);

        bool isDisplayRoleConflict = existing.Exists(o => o.DisplayRoleId == displayRoleId
                                                          && o.OptionId != option?.OptionId);

        if (isKeyConflict)
            return Error.Conflict(description: $"Colour option `{candidate.Key}` is already registered.");

        if (isDisplayRoleConflict)
            return Error.Conflict(description: "Display role is already mapped to a colour option.");

        if (option is null)
        {
            option = candidate;
            await db.RoleColourOptions.AddAsync(option, ct);
            await db.SaveChangesAsync(ct);

            return new Result(option, Created: true, PreviousDisplayRoleId: null);
        }

        ulong previousDisplayRoleId = option.DisplayRoleId;
        ErrorOr<Success> updateResult = option.Update(candidate.Key, candidate.Label, displayRoleId);

        if (updateResult.IsError)
            return updateResult.Errors;

        await db.SaveChangesAsync(ct);

        return new Result(option, Created: false, PreviousDisplayRoleId: previousDisplayRoleId);
    }

    public sealed record Result(RoleColourOption Option, bool Created, ulong? PreviousDisplayRoleId);
}