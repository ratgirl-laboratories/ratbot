using System.Threading.Channels;

namespace RatBot.Application.Reactions;

public sealed record GuildReactionEmoji(ulong GuildId, ulong EmojiId);

public sealed class ReactionQueue
{
    private readonly Channel<GuildReactionEmoji> _channel = Channel.CreateUnbounded<GuildReactionEmoji>(
        new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false,
            AllowSynchronousContinuations = false,
        }
    );
    public ChannelReader<GuildReactionEmoji> Reader => _channel.Reader;

    public ChannelWriter<GuildReactionEmoji> Writer => _channel.Writer;
}
