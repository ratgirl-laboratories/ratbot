using RatBot.Application.Common.Discord;
using RatBot.Application.Features.AdminSend;

namespace RatBot.Interactions.Modules.Admin;

public sealed class DiscordGuildChannelService(IGuild guild) : IDiscordChannelService
{
    public async Task<ErrorOr<ResolvedTextChannel>> GetTextChannelAsync(ulong channelId)
    {
        ErrorOr<ITextChannel> channelResult = await ResolveTextChannelAsync(channelId);

        if (channelResult.IsError)
            return channelResult.Errors;

        ITextChannel channel = channelResult.Value;
        return ErrorOrFactory.From(new ResolvedTextChannel(channel.Id, channel.Mention));
    }

    public async Task<ErrorOr<Success>> ValidateBotCanSendAsync(ulong channelId)
    {
        ErrorOr<ITextChannel> channelResult = await ResolveTextChannelAsync(channelId);

        if (channelResult.IsError)
            return channelResult.Errors;

        IGuildUser currentUser = await guild.GetCurrentUserAsync();
        ChannelPermissions permissions = currentUser.GetPermissions(channelResult.Value);

        if (!permissions.ViewChannel || !permissions.SendMessages)
            return AdminSendErrors.InsufficientPermissions;

        return Result.Success;
    }

    public async Task<ErrorOr<int>> SendMessagesAsync(ulong channelId, IReadOnlyList<string> messages)
    {
        ErrorOr<ITextChannel> channelResult = await ResolveTextChannelAsync(channelId);

        if (channelResult.IsError)
            return channelResult.Errors;

        ITextChannel channel = channelResult.Value;

        foreach (string message in messages)
            await channel.SendMessageAsync(message);

        return messages.Count;
    }

    private async Task<ErrorOr<ITextChannel>> ResolveTextChannelAsync(ulong channelId)
    {
        ITextChannel? channel = await guild.GetTextChannelAsync(channelId);

        return channel is null ? AdminSendErrors.ChannelNotFound : ErrorOrFactory.From(channel);
    }
}
