using Discord;
using Discord.Interactions;
using RatBot.Domain.Entities;
using RatBot.Infrastructure.Services;

namespace RatBot.Interactions;

[Group("virtue-admin", "Virtue administration commands.")]
[DefaultMemberPermissions(GuildPermission.Administrator)]
public sealed class VirtueAdminModule(VirtueRoleTierConfigService virtueRoleTierConfigService) : SlashCommandBase
{
    private const int TotalTiers = 8;

    [SlashCommand("configure-role", "Configure one of the 8 virtue role tiers.")]
    [RequireUserPermission(GuildPermission.Administrator)]
    public async Task ConfigureRoleAsync(int tier, IRole role, int minVirtue, int maxVirtue)
    {
        if (Context.Guild is null)
        {
            await RespondAsync("This command can only be used in a guild.", ephemeral: true);
            return;
        }

        if (tier is < 1 or > TotalTiers)
        {
            await RespondAsync($"Tier must be between 1 and {TotalTiers}.", ephemeral: true);
            return;
        }

        if (minVirtue > maxVirtue)
        {
            await RespondAsync("Min virtue must be less than or equal to max virtue.", ephemeral: true);
            return;
        }

        ulong guildId = Context.Guild.Id;
        List<VirtueRoleTierConfig> existing = await virtueRoleTierConfigService.ListAsync(guildId);

        List<VirtueRoleTierConfig> proposed = existing
            .Where(x => x.TierIndex != tier)
            .Append(
                new VirtueRoleTierConfig
                {
                    GuildId = guildId,
                    TierIndex = tier,
                    RoleId = role.Id,
                    MinVirtue = minVirtue,
                    MaxVirtue = maxVirtue,
                }
            )
            .OrderBy(x => x.MinVirtue)
            .ThenBy(x => x.MaxVirtue)
            .ToList();

        if (proposed.Select(x => x.RoleId).Distinct().Count() != proposed.Count)
        {
            await RespondAsync("Each tier must use a different role.", ephemeral: true);
            return;
        }

        for (int i = 1; i < proposed.Count; i++)
        {
            VirtueRoleTierConfig previous = proposed[i - 1];
            VirtueRoleTierConfig current = proposed[i];

            if (previous.MaxVirtue >= current.MinVirtue)
            {
                await RespondAsync(
                    "Tier ranges cannot overlap. Adjust min/max values and try again.",
                    ephemeral: true
                );
                return;
            }
        }

        await virtueRoleTierConfigService.UpsertAsync(guildId, tier, role.Id, minVirtue, maxVirtue);

        int configuredCount = proposed.Select(x => x.TierIndex).Distinct().Count();
        await RespondAsync(
            $"Configured tier `{tier}`: {role.Mention} for range `{minVirtue}`..`{maxVirtue}`. Configured tiers: `{configuredCount}/{TotalTiers}`.",
            ephemeral: true
        );
    }
}
