namespace RatBot.Domain.SecretRole;

public sealed class SecretRoleSetting
{
    public required ulong GuildId { get; set; }

    public required ulong RoleId { get; set; }
}
