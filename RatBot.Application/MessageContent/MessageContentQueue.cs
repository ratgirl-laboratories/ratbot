using System.Threading.Channels;

namespace RatBot.Application.MessageContent;

public sealed class MessageContentQueue
{
    private readonly Channel<string> _channel = Channel.CreateUnbounded<string>(
        new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false,
            AllowSynchronousContinuations = false,
        }
    );
    public ChannelReader<string> Reader => _channel.Reader;

    public ChannelWriter<string> Writer => _channel.Writer;
}
