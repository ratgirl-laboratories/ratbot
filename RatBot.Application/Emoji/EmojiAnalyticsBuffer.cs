using System.Threading.Channels;

namespace RatBot.Application.Emoji;

public sealed class EmojiAnalyticsBuffer
{
    private readonly Channel<string> _channel = Channel.CreateUnbounded<string>(
        new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false,
            AllowSynchronousContinuations = false
        }
    );

    public ChannelWriter<string> Writer => _channel.Writer;
    public ChannelReader<string> Reader => _channel.Reader;
}