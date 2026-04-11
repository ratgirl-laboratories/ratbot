using System.Text;
using RatBot.Application.Features.Emoji;
using RatBot.Domain.Entities;

namespace RatBot.Interactions.Modules.Emoji;

[Group("emoji", "Emoji analytics commands.")]
[DefaultMemberPermissions(GuildPermission.MuteMembers)]
public sealed class EmojiModule(EmojiAnalyticsService emojiAnalyticsService, DiscordSocketClient discordClient)
    : SlashCommandBase
{
    [SlashCommand("usage", "Show the top 25 emojis by usage.")]
    public Task UsageAsync() => ReplyAsync(GetUsageResponseAsync, true);

    private async Task<string> GetUsageResponseAsync()
    {
        List<EmojiUsageCount> topUsage = await emojiAnalyticsService.GetTopUsageAsync();

        if (topUsage.Count == 0)
            return "No emoji usage has been recorded yet.";

        StringBuilder text = new StringBuilder("Top emoji usage:\n");

        foreach (EmojiUsageCount row in topUsage)
            text.AppendLine($"{FormatEmojiForDisplay(row.EmojiId)}: {row.UsageCount}");
        
        return text.ToString();
    }

    private string FormatEmojiForDisplay(string emojiId)
    {
        if (string.IsNullOrWhiteSpace(emojiId))
            return "[unknown]";

        if (!ulong.TryParse(emojiId, out ulong parsedEmojiId))
            return emojiId;

        foreach (SocketGuild guild in discordClient.Guilds)
        {
            GuildEmote? guildEmote = guild.Emotes.FirstOrDefault(x => x.Id == parsedEmojiId);

            if (guildEmote is not null)
                return guildEmote.ToString();
        }

        return $"[custom:{parsedEmojiId}]";
    }
}