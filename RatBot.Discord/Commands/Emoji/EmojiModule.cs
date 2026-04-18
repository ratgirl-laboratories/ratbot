using System.Text;
using RatBot.Application.Emoji;
using RatBot.Domain.Emoji;

namespace RatBot.Discord.Commands.Emoji;

[Group("emoji", "Emoji analytics commands.")]
[DefaultMemberPermissions(GuildPermission.MuteMembers)]
public sealed class EmojiModule(EmojiAnalyticsService emojiAnalyticsService, DiscordSocketClient discordClient)
    : SlashCommandBase
{
    [SlashCommand("usage", "Show the top 25 emojis by usage.")]
    public async Task UsageAsync()
    {
        ErrorOr<List<EmojiUsageCount>> topUsageResult = await emojiAnalyticsService.GetTopUsageAsync();

        await topUsageResult.SwitchFirstAsync(
            async topUsage =>
            {
                StringBuilder text = new StringBuilder("Top emoji usage:\n");

                foreach (EmojiUsageCount row in topUsage)
                    text.AppendLine($"{FormatEmojiForDisplay(row.EmojiId)}: {row.UsageCount}");

                await RespondAsync(text.ToString(), ephemeral: true);
            },
            async error => await RespondAsync(error.Description, ephemeral: true)
        );
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