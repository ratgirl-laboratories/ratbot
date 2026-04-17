namespace RatBot.Domain.Primitives;

public record SnowflakeBase
{
    protected SnowflakeBase(ulong id)
    {
        Id = id;
    }

    private const ulong DiscordEpoch = 1420070400000;

    public ulong Id { get; init; }

    public DateTime Timestamp => DateTimeOffset.FromUnixTimeMilliseconds((long)((Id >> 22) + DiscordEpoch)).DateTime;
    public override string ToString() => Id.ToString();
}