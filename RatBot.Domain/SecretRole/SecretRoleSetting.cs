namespace RatBot.Domain.SecretRole;

public sealed class SecretRoleSetting
{
    public const int SingletonId = 1;

    public required ulong GuildId { get; set; }

    public int Id { get; private set; } = SingletonId;

    public required ulong RoleId { get; set; }
}
