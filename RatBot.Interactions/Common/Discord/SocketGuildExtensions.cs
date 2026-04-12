namespace RatBot.Interactions.Common.Discord;

public static class SocketGuildExtensions
{
    extension(SocketGuild guild)
    {
        public ErrorOr<SocketTextChannel> FetchTextChannel(ulong channelId)
        {
            SocketTextChannel? channel = guild.GetTextChannel(channelId);

            if (channel is null)
                return Error.NotFound(description: "Channel not found.");

            return channel;
        }

        public ErrorOr<SocketGuildUser> FetchUser(ulong userId)
        {
            SocketGuildUser? user = guild.GetUser(userId);

            if (user is null)
                return Error.NotFound(description: "User not found.");

            return user;
        }

        public ErrorOr<SocketRole> FetchRole(ulong roleId)
        {
            SocketRole? role = guild.GetRole(roleId);

            if (role is null)
                return Error.NotFound(description: "Role not found.");

            return role;
        }
    }
}