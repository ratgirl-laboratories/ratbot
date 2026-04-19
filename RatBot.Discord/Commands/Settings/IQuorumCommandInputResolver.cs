using System.Collections.Immutable;

namespace RatBot.Discord.Commands.Settings;

public interface IQuorumCommandInputResolver
{
    ErrorOr<QuorumTarget> ResolveTarget(SocketGuild guild, IChannel target);

    ErrorOr<ImmutableArray<SocketRole>> ResolveRoles(SocketGuild guild, string rolesCsv);
}
