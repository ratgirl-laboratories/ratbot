namespace RatBot.Domain.Primitives;

public record RoleSnowflake(ulong Id) : SnowflakeBase(Id);