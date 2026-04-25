using RatBot.Infrastructure.Data;

namespace RatBot.Infrastructure.RoleColours;

public static class SetNoColourPreference
{

    public static async Task<Result> ExecuteAsync(
        BotDbContext db,
        Command command,
        CancellationToken ct)
    {
        MemberColourPreference? pref = await db.MemberColourPreferences
            .SingleOrDefaultAsync(p => p.UserId == command.UserId, ct);

        if (pref is null)
        {
            pref = MemberColourPreference.CreateNoColour(command.UserId);
            await db.MemberColourPreferences.AddAsync(pref, ct);
        }
        else
        {
            pref.SelectNoColour();
        }

        await db.SaveChangesAsync(ct);
        return Result.Ok();
    }
    public sealed record Command(ulong UserId);

    public sealed record Result(bool Success, string? ErrorDescription)
    {
        public static Result Ok() => new Result(true, null);
        public static Result Fail(string description) => new Result(false, description);
    }
}
