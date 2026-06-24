namespace RatBot.Domain.SecretRole;

public sealed class SecretRoleSetting
{
    public const int SingletonId = 1;

    public int Id { get; private set; } = SingletonId;

    public required ulong GuildId { get; set; }

    public required ulong RoleId { get; set; }
}
