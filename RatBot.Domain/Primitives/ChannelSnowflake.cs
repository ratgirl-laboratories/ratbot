namespace RatBot.Domain.Primitives;

public sealed record ChannelSnowflake(ulong Id) : SnowflakeBase(Id);
