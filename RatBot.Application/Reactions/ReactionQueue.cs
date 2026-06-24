using System.Threading.Channels;

namespace RatBot.Application.Reactions;

public sealed class ReactionQueue
{
    private readonly Channel<ulong> _channel = Channel.CreateUnbounded<ulong>(
        new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false,
            AllowSynchronousContinuations = false,
        }
    );

    public ChannelWriter<ulong> Writer => _channel.Writer;
    public ChannelReader<ulong> Reader => _channel.Reader;
}
