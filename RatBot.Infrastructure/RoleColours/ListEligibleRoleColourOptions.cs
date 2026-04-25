using RatBot.Infrastructure.Data;

namespace RatBot.Infrastructure.RoleColours;

/// <summary>
/// Query for listing enabled RoleColourOptions which the member is currently entitled to via SCR membership.
/// </summary>
public static class ListEligibleRoleColourOptions
{

    public static Task<IReadOnlyList<RoleColourOption>> ExecuteAsync(
        BotDbContext db,
        Query query,
        CancellationToken ct)
    {
        IReadOnlyCollection<ulong> roleIds = query.CurrentMemberRoleIds;

        IQueryable<RoleColourOption> q = db.RoleColourOptions
            .AsNoTracking()
            .Where(o => o.IsEnabled && roleIds.Contains(o.SourceRoleId))
            .OrderBy(o => o.Label)
            .ThenBy(o => o.NormalisedKey);

        return q.ToListAsync(ct).ContinueWith<IReadOnlyList<RoleColourOption>>(t => t.Result, ct);
    }
    public sealed record Query(
        IReadOnlyCollection<ulong> CurrentMemberRoleIds
    );
}
