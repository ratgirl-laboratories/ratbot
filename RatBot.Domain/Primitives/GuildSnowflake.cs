namespace RatBot.Domain.Primitives;

public sealed record GuildSnowflake(ulong Id) : SnowflakeBase(Id);
