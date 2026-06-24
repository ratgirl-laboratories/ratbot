using Microsoft.Extensions.Options;
using RatBot.Application.Common.Interfaces;
using RatBot.Discord.Configuration;

namespace RatBot.Discord.Commands.Emoji;

public sealed class TrackedEmojiCatalog(DiscordSocketClient discordClient, IOptions<DiscordOptions> options) : ITrackedEmojiCatalog
{
    public bool TryGetTrackedEmojiIds(out IReadOnlyCollection<ulong> emojiIds)
    {
        SocketGuild? guild = discordClient.GetGuild(options.Value.GuildId);

        if (guild is null)
        {
            emojiIds = [];
            return false;
        }

        emojiIds = guild.Emotes.Select(emote => emote.Id).ToArray();

        return true;
    }
}
