using System.Text.RegularExpressions;
using Discord;
using Discord.Interactions;
using RatBot.Domain.Entities;
using RatBot.Infrastructure.Services;

// ReSharper disable UnusedType.Global

namespace RatBot.Interactions;

[Group("virtue", "Manage virtue emoji mappings.")]
public sealed partial class VirtueModule(
    EmojiVirtueService emojiVirtueService,
    UserVirtueService userVirtueService,
    VirtueRoleTierConfigService virtueRoleTierConfigService
) : SlashCommandBase
{
    [SlashCommand("add-emoji", "Add or update an emoji virtue mapping.")]
    [RequireUserPermission(GuildPermission.Administrator)]
    public async Task AddEmojiAsync(string emoji, int virtue)
    {
        if (virtue is < -5 or > 5)
        {
            await RespondAsync("Virtue must be between -5 and 5.", ephemeral: true);
            return;
        }

        string emojiId = ResolveEmojiId(emoji);

        if (string.IsNullOrWhiteSpace(emojiId))
        {
            await RespondAsync(
                "Invalid emoji input. Use a unicode emoji, custom emoji mention (`<:name:id>`), or emoji ID.",
                ephemeral: true
            );

            return;
        }

        await emojiVirtueService.UpsertVirtueAsync(emojiId, virtue);

        await RespondAsync($"Saved virtue mapping: `{emojiId}` => `{virtue}`.", ephemeral: true);
    }

    [SlashCommand("user-virtue", "Get a user's tracked virtue.")]
    [RequireUserPermission(GuildPermission.BanMembers)]
    public async Task UserVirtueAsync(IUser user)
    {
        int? virtue = await userVirtueService.TryGetVirtueAsync(user.Id);
        if (virtue is null)
        {
            await RespondAsync($"{user.Mention} does not have a recorded virtue yet.", ephemeral: true);
            return;
        }

        await RespondAsync($"{user.Mention} has a virtue of `{virtue.Value}`.", ephemeral: true);
    }

    [SlashCommand("show", "Show your current virtue.")]
    [RequireUserPermission(GuildPermission.Administrator)]
    public async Task ShowAsync()
    {
        if (!await TryDeferEphemeralAsync())
            return;

        int? virtue = await userVirtueService.TryGetVirtueAsync(Context.User.Id);
        if (virtue is null)
        {
            await SendEphemeralAsync("You do not have a recorded virtue yet.");
            return;
        }

        await SendEphemeralAsync($"Your virtue is `{virtue.Value}`.");
    }

    [SlashCommand("configure-role", "Configure one of the 7 virtue role tiers.")]
    [RequireUserPermission(GuildPermission.Administrator)]
    public async Task ConfigureRoleAsync(int tier, IRole role, int minVirtue, int maxVirtue)
    {
        if (Context.Guild is null)
        {
            await RespondAsync("This command can only be used in a guild.", ephemeral: true);
            return;
        }

        if (tier is < 1 or > 7)
        {
            await RespondAsync("Tier must be between 1 and 7.", ephemeral: true);
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
            $"Configured tier `{tier}`: {role.Mention} for range `{minVirtue}`..`{maxVirtue}`. Configured tiers: `{configuredCount}/7`.",
            ephemeral: true
        );
    }

    private static string ResolveEmojiId(string input)
    {
        string trimmed = input.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
            return string.Empty;

        if (ulong.TryParse(trimmed, out _))
            return trimmed;

        Match customMatch = GetEmojiRegex().Match(trimmed);

        return customMatch.Success ? customMatch.Groups[1].Value : trimmed;
    }

    [GeneratedRegex("^<a?:[^:]+:(\\d+)>$")]
    private static partial Regex GetEmojiRegex();
}
