using System.Collections.Immutable;
using RatBot.Application.RoleColours;

namespace RatBot.Discord.Handlers;

public sealed class RoleColourReconciler(IServiceScopeFactory scopeFactory, ILogger logger) : IRoleColourReconciler
{
    private const string NoneLogValue = "(none)";

    public async Task ReconcileMemberAsync(IGuild guild, ulong userId, CancellationToken ct)
    {
        IGuildUser? member = await guild.GetUserAsync(userId);

        if (member is null)
        {
            logger.Debug("role_colour_reconcile member_missing guild_id={GuildId} user_id={UserId}", guild.Id, userId);
            return;
        }

        using IServiceScope scope = scopeFactory.CreateScope();
        IRoleColourRepository roleColours = scope.ServiceProvider.GetRequiredService<IRoleColourRepository>();

        ImmutableArray<RoleColourOption> options = await roleColours.GetOptionsAsync(ct);
        MemberColourPreference preference = await roleColours.GetPreferenceAsync(userId, ct);
        ImmutableHashSet<ulong> currentRoleIds = member.RoleIds.ToImmutableHashSet();
        ImmutableDictionary<ulong, int> sourceRolePositions = CollectRolePositions(guild, currentRoleIds);
        RoleColourPlan plan = RoleColourRules.CreatePlan(options, preference, currentRoleIds, sourceRolePositions);

        if (plan.IsNoOp)
        {
            logger.Debug(
                "role_colour_reconcile noop guild_id={GuildId} user_id={UserId} target_dcr={TargetDcr}",
                guild.Id,
                userId,
                plan.TargetDisplayRoleId?.ToString() ?? NoneLogValue
            );

            return;
        }

        await ApplyPlanAsync(guild, member, plan, ct);
    }

    private static ImmutableDictionary<ulong, int> CollectRolePositions(IGuild guild, IReadOnlyCollection<ulong> roleIds)
    {
        // Collect Discord positions so the domain rule can break fallback ties without Discord types.
        ImmutableDictionary<ulong, int>.Builder positions = ImmutableDictionary.CreateBuilder<ulong, int>();

        foreach (ulong roleId in roleIds)
        {
            IRole? role = guild.GetRole(roleId);

            if (role is not null)
                positions[roleId] = role.Position;
        }

        return positions.ToImmutable();
    }

    private async Task ApplyPlanAsync(IGuild guild, IGuildUser member, RoleColourPlan plan, CancellationToken ct)
    {
        logger.Debug(
            "role_colour_reconcile applying guild_id={GuildId} user_id={UserId} target_dcr={TargetDcr} add_count={AddCount} remove_count={RemoveCount}",
            guild.Id,
            member.Id,
            plan.TargetDisplayRoleId?.ToString() ?? NoneLogValue,
            plan.RoleIdsToAdd.Length,
            plan.RoleIdsToRemove.Length
        );

        try
        {
            // Remove first, then add target to avoid multiple DCRs simultaneously
            foreach (ulong roleId in plan.RoleIdsToRemove)
                await member.RemoveRoleAsync(roleId, new RequestOptions { CancelToken = ct });

            foreach (ulong roleId in plan.RoleIdsToAdd)
                await member.AddRoleAsync(roleId, new RequestOptions { CancelToken = ct });

            logger.Debug(
                "role_colour_reconcile done guild_id={GuildId} user_id={UserId} added=[{Added}] removed=[{Removed}]",
                guild.Id,
                member.Id,
                string.Join(',', plan.RoleIdsToAdd),
                string.Join(',', plan.RoleIdsToRemove)
            );
        }
        catch (Exception ex)
        {
            logger.Error(
                ex,
                "role_colour_reconcile failed guild_id={GuildId} user_id={UserId} target_dcr={TargetDcr}",
                guild.Id,
                member.Id,
                plan.TargetDisplayRoleId?.ToString() ?? NoneLogValue
            );
        }
    }
}
