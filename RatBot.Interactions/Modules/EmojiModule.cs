using System.Text;
using RatBot.Application.Features.Emoji;
using RatBot.Domain.Entities;

namespace RatBot.Interactions.Modules;

[Group("emoji", "Emoji analytics commands.")]
[DefaultMemberPermissions(GuildPermission.MuteMembers)]
public sealed class EmojiModule(EmojiAnalyticsService emojiAnalyticsService, DiscordSocketClient discordClient)
    : SlashCommandBase
{
    [SlashCommand("usage", "Show the top 25 emojis by usage.")]
    public async Task UsageAsync()
    {
        List<EmojiUsageCount> topUsage = await emojiAnalyticsService.GetTopUsageAsync();

        if (topUsage.Count == 0)
        {
            await RespondAsync("No emoji usage has been recorded yet.", ephemeral: true);
            return;
        }

        StringBuilder text = new StringBuilder("Top emoji usage:\n");

        foreach (EmojiUsageCount row in topUsage)
            text.AppendLine($"{FormatEmojiForDisplay(row.EmojiId)}: {row.UsageCount}");
        
        await RespondAsync(text.ToString(), ephemeral: true);
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
