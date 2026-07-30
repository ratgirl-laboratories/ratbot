using RatBot.Application.Common.Interfaces;

namespace RatBot.Commands.Emoji;

public sealed class TrackedEmojiCatalog(DiscordSocketClient discordClient, IOptions<EmojiAnalyticsOptions> options) : ITrackedEmojiCatalog
{
    private readonly EmojiAnalyticsOptions _options = options.Value;

    public bool TryGetTrackedEmojiIds(ulong guildId, out IReadOnlyCollection<ulong> emojiIds)
    {
        if (!_options.IsEnabled(guildId))
        {
            emojiIds = [];
            return false;
        }

        SocketGuild? guild = discordClient.GetGuild(guildId);

        if (guild is null)
        {
            emojiIds = [];
            return false;
        }

        emojiIds = guild.Emotes.Select(emote => emote.Id).ToArray();

        return true;
    }
}
