using System.Text.RegularExpressions;
using Discord;
using Discord.Interactions;
using RatBot.Infrastructure.Services;

// ReSharper disable UnusedType.Global

namespace RatBot.Interactions;

[Group("virtue", "Manage virtue emoji mappings.")]
public sealed partial class VirtueModule(
    EmojiVirtueService emojiVirtueService,
    UserVirtueService userVirtueService
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
