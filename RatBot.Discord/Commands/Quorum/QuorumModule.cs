namespace RatBot.Discord.Commands.Quorum;

[Group("quorum", "Quorum commands. Group restricted to moderators by default.")]
[DefaultMemberPermissions(GuildPermission.MuteMembers)]
public sealed class QuorumModule(ILogger logger, QuorumSettingsService quorumSettingsService) : SlashCommandBase
{
    private readonly ILogger _logger = logger.ForContext<QuorumModule>();

    private static string DescribeError(Error error) =>
        error.Type == ErrorType.NotFound
            ? "No quorum settings found for this channel or category."
            : error.Description;

    [SlashCommand("count", "Count the number of members needed for quorum.")]
    [RequireUserPermission(GuildPermission.SendPolls)]
    public async Task CountAsync()
    {
        if (Context.Channel is not ITextChannel currentChannel)
        {
            await RespondAsync("This command can only be used in a text channel.", ephemeral: true);
            return;
        }

        ICategoryChannel? category = await currentChannel.GetCategoryAsync();

        ErrorOr<QuorumSettings> configResult = await quorumSettingsService.GetEffectiveAsync(
            currentChannel.GuildId,
            currentChannel.Id,
            category?.Id);

        await configResult.SwitchFirstAsync(
            async config => await RespondWithQuorumCountAsync(config),
            async error => await RespondAsync(DescribeError(error), ephemeral: true));
    }

    private async Task RespondWithQuorumCountAsync(QuorumSettings config)
    {
        logger.Debug("Quorum settings: {Config}", config);

        SocketGuild guild = Context.Guild!;
        SocketRole[] roles = config.RoleIds.Select(guild.GetRole).Where(role => role is not null).ToArray()!;

        HashSet<ulong> usersWithRoles = roles.SelectMany(x => x.Members).Select(y => y.Id).ToHashSet();

        int quorumCount = QuorumCalculator.CalculateRequiredMemberCount(usersWithRoles.Count, config.QuorumProportion);

        _logger.Debug(
            "Members with roles {RoleIds}: {UsersWithRoles}, quorum count: {QuorumCount}, proportion: {ConfigQuorumProportion}",
            config.RoleIds,
            usersWithRoles,
            quorumCount,
            config.QuorumProportion);

        await RespondAsync("Quorum count for this channel: " + quorumCount);
    }
}