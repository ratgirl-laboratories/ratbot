using System.Text.RegularExpressions;
using Discord;
using Discord.Interactions;
using RatBot.Infrastructure.Services;

namespace RatBot.Interactions;

[Group("reaction-score", "Manage reaction score emoji mappings.")]
public sealed partial class ReactionScoreModule(ReactionEmojiScoreService reactionEmojiScoreService) : SlashCommandBase
{
    [SlashCommand("add-emoji", "Add or update an emoji score mapping.")]
    [RequireUserPermission(GuildPermission.Administrator)]
    public async Task AddEmojiAsync(string emoji, int score)
    {
        if (score is < -5 or > 5)
        {
            await RespondAsync("Score must be between -5 and 5.", ephemeral: true);
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

        await reactionEmojiScoreService.UpsertAsync(emojiId, score);
        await RespondAsync($"Saved mapping: `{emojiId}` => `{score}`.", ephemeral: true);
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
