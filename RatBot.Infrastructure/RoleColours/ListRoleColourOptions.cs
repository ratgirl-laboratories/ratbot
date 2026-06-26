namespace RatBot.Infrastructure.RoleColours;

public static class ListRoleColourOptions
{
    public static async Task<IReadOnlyList<RoleColourOption>> ExecuteAsync(BotDbContext db, Query query, CancellationToken ct)
    {
        IQueryable<RoleColourOption> q = db.RoleColourOptions.AsNoTracking().Where(o => query.IncludeDisabled || o.IsEnabled).OrderBy(o => o.Key);

        return await q.ToListAsync(ct);
    }

    public sealed record Query(bool IncludeDisabled);
}
