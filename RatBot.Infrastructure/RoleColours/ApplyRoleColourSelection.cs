using RatBot.Infrastructure.Data;

namespace RatBot.Infrastructure.RoleColours;

public static class ApplyRoleColourSelection
{

    public async static Task<Result> ExecuteAsync(
        BotDbContext db,
        Command command,
        CancellationToken ct)
    {
        // Load the selected option
        RoleColourOption? option = await db.RoleColourOptions
            .AsNoTracking()
            .SingleOrDefaultAsync(o => o.OptionId == command.SelectedOptionId, ct);

        if (option is null)
            return Result.Fail("That colour is no longer available to you.");

        if (!option.IsEnabled)
            return Result.Fail("That colour is no longer available to you.");

        if (!command.CurrentMemberRoleIds.Contains(option.SourceRoleId))
            return Result.Fail("That colour is no longer available to you.");

        // Upsert preference
        MemberColourPreference? pref = await db.MemberColourPreferences
            .SingleOrDefaultAsync(p => p.UserId == command.UserId, ct);

        if (pref is null)
        {
            pref = MemberColourPreference.CreateForOption(command.UserId, command.SelectedOptionId);
            await db.MemberColourPreferences.AddAsync(pref, ct);
        }
        else
        {
            pref.SelectOption(command.SelectedOptionId);
        }

        await db.SaveChangesAsync(ct);
        return Result.Ok();
    }
    public sealed record Command(
        ulong UserId,
        RoleColourOption.Id SelectedOptionId,
        IReadOnlyCollection<ulong> CurrentMemberRoleIds
    );

    public sealed record Result(bool Success, string? ErrorDescription)
    {
        public static Result Ok() => new Result(true, null);
        public static Result Fail(string description) => new Result(false, description);
    }
}
