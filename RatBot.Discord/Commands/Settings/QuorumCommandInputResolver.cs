using System.Collections.Immutable;

namespace RatBot.Discord.Commands.Settings;

public sealed class QuorumCommandInputResolver : IQuorumCommandInputResolver
{
    public ErrorOr<QuorumTarget> ResolveTarget(SocketGuild guild, IChannel target)
    {
        if (target is not SocketGuildChannel guildChannel || guildChannel.Guild.Id != guild.Id)
            return Error.Validation(description: "Invalid guild channel provided.");

        QuorumSettingsType? targetType = guildChannel.ChannelType switch
        {
            ChannelType.Text => QuorumSettingsType.Channel,
            ChannelType.Category => QuorumSettingsType.Category,
            _ => null,
        };

        if (targetType is null)
            return Error.Validation(description: "Invalid channel type for quorum settings.");

        return QuorumTarget.Create(guild.Id, targetType.Value, guildChannel.Id);
    }

    public ErrorOr<ImmutableArray<SocketRole>> ResolveRoles(SocketGuild guild, string rolesCsv)
    {
        string[] entries = rolesCsv.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        ImmutableArray<SocketRole>.Builder resolvedRoles = ImmutableArray.CreateBuilder<SocketRole>();

        foreach (string entry in entries)
        {
            ulong roleId;

            if (MentionUtils.TryParseRole(entry, out ulong mentionRoleId))
                roleId = mentionRoleId;
            else if (ulong.TryParse(entry, out ulong parsedRoleId))
                roleId = parsedRoleId;
            else
                return Error.Validation(description: $"Invalid role entry: \"{entry}\".");

            SocketRole? role = guild.GetRole(roleId);

            if (role is null)
                return Error.Validation(description: $"Role not found in this guild: \"{entry}\".");

            resolvedRoles.Add(role);
        }

        return resolvedRoles.ToImmutable();
    }
}
