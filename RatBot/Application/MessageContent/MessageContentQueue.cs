using System.Threading.Channels;

namespace RatBot.Application.MessageContent;

public sealed record GuildMessageContent(ulong GuildId, string Content);

public sealed class MessageContentQueue
{
    private readonly Channel<GuildMessageContent> _channel = Channel.CreateUnbounded<GuildMessageContent>(
        new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false,
            AllowSynchronousContinuations = false,
        }
    );
    public ChannelReader<GuildMessageContent> Reader => _channel.Reader;

    public ChannelWriter<GuildMessageContent> Writer => _channel.Writer;
}
