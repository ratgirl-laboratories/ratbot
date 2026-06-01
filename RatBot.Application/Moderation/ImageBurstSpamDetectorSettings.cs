namespace RatBot.Application.Moderation;

public sealed class ImageBurstSpamDetectorSettings(ImageBurstSpamDetectorOptions initialOptions)
{
    private readonly Lock _gate = new Lock();
    private ImageBurstSpamDetectorOptions _current = initialOptions;

    public ImageBurstSpamDetectorSettings()
        : this(new ImageBurstSpamDetectorOptions())
    {
    }

    public ImageBurstSpamDetectorOptions Current
    {
        get
        {
            lock (_gate)
                return _current;
        }
    }

    public void Update(int window, int distinctChannelThreshold)
    {
        lock (_gate)
        {
            _current = _current with
            {
                Window = window,
                DistinctChannelThreshold = distinctChannelThreshold,
            };
        }
    }
}
