namespace RatBot.Domain.Primitives;

public sealed record UserSnowflake(ulong Id) : SnowflakeBase(Id);
