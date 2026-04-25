using Microsoft.EntityFrameworkCore;
using RatBot.Infrastructure.Data;

namespace RatBot.Discord.Handlers;

/// <summary>
/// Reconciles a member's Display Colour Role (DCR) based on their stored preference,
/// currently enabled colour options, and current Source Colour Roles (SCRs).
/// </summary>
public sealed class RoleColourReconciler(IServiceScopeFactory scopeFactory, ILogger logger) : IRoleColourReconciler
{
    private const string NoneLogValue = "(none)";

    private static ulong? ResolveConfiguredTargetDcr(
        IReadOnlyCollection<ulong> currentRoles,
        MemberColourPreference? preference,
        IReadOnlyCollection<RoleColourOption> enabledOptions)
    {
        if (preference is not { Kind: MemberColourPreferenceKind.ConfiguredOption, SelectedOptionId: not null })
            return null;

        RoleColourOption.Id selectedId = preference.SelectedOptionId.Value;
        RoleColourOption? selected = enabledOptions.SingleOrDefault(o => o.OptionId.Equals(selectedId));

        return selected is not null && currentRoles.Contains(selected.SourceRoleId)
            ? selected.DisplayRoleId
            : null;
    }

    private static HashSet<ulong> GetDisplayRolesToRemove(HashSet<ulong> currentDcrs, ulong? targetDcr) =>
        currentDcrs
            .Where(roleId => roleId != targetDcr)
            .ToHashSet();

    private static List<ulong> GetDisplayRolesToAdd(IReadOnlyCollection<ulong> currentRoles, ulong? targetDcr) =>
        targetDcr.HasValue && !currentRoles.Contains(targetDcr.Value)
            ? new List<ulong> { targetDcr.Value }
            : new List<ulong>();

    public async Task ReconcileMemberAsync(IGuild guild, ulong userId, CancellationToken ct)
    {
        using IServiceScope scope = scopeFactory.CreateScope();
        BotDbContext db = scope.ServiceProvider.GetRequiredService<BotDbContext>();

        MemberColourPreference? prefTracked = await db.MemberColourPreferences
            .SingleOrDefaultAsync(p => p.UserId == userId, ct);

        // Load all options; we use enabled ones for selection logic, but remove any DCRs from all configured options
        List<RoleColourOption> allOptions = await db.RoleColourOptions
            .AsNoTracking()
            .ToListAsync(ct);
        List<RoleColourOption> enabledOptions = allOptions
            .Where(o => o.IsEnabled)
            .ToList();

        IGuildUser? member = await guild.GetUserAsync(userId);

        if (member is null)
        {
            logger.Debug("role_colour_reconcile member_missing guild_id={GuildId} user_id={UserId}", guild.Id, userId);
            return;
        }

        IReadOnlyCollection<ulong> currentRoles = member.RoleIds;

        // If the user explicitly selected NoColour, do not apply fallback; remove all DCRs instead
        ulong? targetDcr = prefTracked?.IsNoColourSelected == true
            ? null
            : ResolveTargetDcr(guild, userId, currentRoles, prefTracked, enabledOptions);

        // Remove any DCRs that belong to any configured option (enabled or disabled)
        HashSet<ulong> dcrSet = allOptions
            .Select(colourOption => colourOption.DisplayRoleId)
            .ToHashSet();

        HashSet<ulong> currentDcrs = currentRoles
            .Where(dcrSet.Contains)
            .ToHashSet();

        HashSet<ulong> toRemove = GetDisplayRolesToRemove(currentDcrs, targetDcr);
        List<ulong> toAdd = GetDisplayRolesToAdd(currentRoles, targetDcr);

        if (toAdd.Count == 0 && toRemove.Count == 0)
        {
            logger.Debug(
                "role_colour_reconcile noop guild_id={GuildId} user_id={UserId} pref_kind={PrefKind} target_dcr={TargetDcr}",
                guild.Id,
                userId,
                prefTracked?.Kind.ToString() ?? NoneLogValue,
                targetDcr?.ToString() ?? NoneLogValue
            );

            return;
        }

        logger.Debug(
            "role_colour_reconcile applying guild_id={GuildId} user_id={UserId} target_dcr={TargetDcr} add_count={AddCount} remove_count={RemoveCount}",
            guild.Id,
            userId,
            targetDcr?.ToString() ?? NoneLogValue,
            toAdd.Count,
            toRemove.Count
        );

        try
        {
            // Remove first, then add target to avoid multiple DCRs simultaneously
            foreach (ulong roleId in toRemove)
                await member.RemoveRoleAsync(roleId, new RequestOptions { CancelToken = ct });

            foreach (ulong roleId in toAdd)
                await member.AddRoleAsync(roleId, new RequestOptions { CancelToken = ct });

            logger.Debug(
                "role_colour_reconcile done guild_id={GuildId} user_id={UserId} added=[{Added}] removed=[{Removed}]",
                guild.Id,
                userId,
                string.Join(',', toAdd),
                string.Join(',', toRemove)
            );
        }
        catch (Exception ex)
        {
            logger.Error(
                ex,
                "role_colour_reconcile failed guild_id={GuildId} user_id={UserId} target_dcr={TargetDcr}",
                guild.Id,
                userId,
                targetDcr?.ToString() ?? NoneLogValue);
        }
    }

    private ulong? ResolveTargetDcr(
        IGuild guild,
        ulong userId,
        IReadOnlyCollection<ulong> currentRoles,
        MemberColourPreference? preference,
        IReadOnlyCollection<RoleColourOption> enabledOptions) =>
        ResolveConfiguredTargetDcr(currentRoles, preference, enabledOptions)
        ?? ResolveFallbackTargetDcr(guild, userId, currentRoles, enabledOptions);

    private ulong? ResolveFallbackTargetDcr(
        IGuild guild,
        ulong userId,
        IReadOnlyCollection<ulong> currentRoles,
        IReadOnlyCollection<RoleColourOption> enabledOptions)
    {
        RoleColourOption? best = enabledOptions
            .Where(o => currentRoles.Contains(o.SourceRoleId))
            .Select(o => new
            {
                Option = o,
                Role = guild.GetRole(o.SourceRoleId)
            })
            .OrderByDescending(x => x.Role?.Position ?? int.MinValue)
            .ThenBy(x => x.Option.Label, StringComparer.OrdinalIgnoreCase)
            .Select(x => x.Option)
            .FirstOrDefault();

        if (best is null)
        {
            logger.Debug(
                "role_colour_reconcile fallback_transient_none guild_id={GuildId} user_id={UserId}",
                guild.Id,
                userId);

            return null;
        }

        logger.Debug(
            "role_colour_reconcile fallback_transient_selected guild_id={GuildId} user_id={UserId} option_id={OptionId} dcr={Dcr}",
            guild.Id,
            userId,
            best.OptionId.Value,
            best.DisplayRoleId);

        return best.DisplayRoleId;
    }
}