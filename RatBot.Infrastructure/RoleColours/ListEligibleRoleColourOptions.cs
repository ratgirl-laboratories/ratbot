using System.Collections.Immutable;

namespace RatBot.Infrastructure.RoleColours;

/// <summary>
///     Query for listing enabled RoleColourOptions which the member is currently entitled to via SCR membership.
/// </summary>
public static class ListEligibleRoleColourOptions
{
    public static async Task<IReadOnlyList<RoleColourOption>> ExecuteAsync(BotDbContext db, Query query, CancellationToken ct)
    {
        IReadOnlyCollection<ulong> roleIds = query.CurrentMemberRoleIds;

        RoleColourOption[] options = await db
            .RoleColourOptions.AsNoTracking()
            .Where(o => o.IsEnabled && roleIds.Contains(o.SourceRoleId))
            .OrderBy(o => o.Label)
            .ThenBy(o => o.NormalisedKey)
            .ToArrayAsync(ct);

        return options.ToImmutableArray();
    }

    public sealed record Query(IReadOnlyCollection<ulong> CurrentMemberRoleIds);
}
