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
            async config => await RespondAsync(GetQuorumCount(config), ephemeral: false),
            async error => await RespondAsync(DescribeError(error), ephemeral: true)
        );
    }

    private string GetQuorumCount(QuorumSettings config)
    {
        _logger.Debug("Quorum settings: {Config}", config);

        SocketGuild guild = Context.Guild!;

        SocketRole[] roles = config.Roles.Select(role => guild.GetRole(role.Id))
            .Where(role => role is not null)
            .ToArray();

        HashSet<ulong> usersWithRoles = roles.SelectMany(role => role.Members).Select(user => user.Id).ToHashSet();

        if (!guild.HasAllMembers)
            _logger.Warning(
                "Quorum count is using an incomplete guild member cache. GuildId={GuildId}, DownloadedMemberCount={DownloadedMemberCount}, MemberCount={MemberCount}",
                guild.Id,
                guild.DownloadedMemberCount,
                guild.MemberCount
            );

        int quorumCount = QuorumCalculator.CalculateRequiredMemberCount(usersWithRoles.Count, config.QuorumProportion);

        _logger.Debug(
            "Members with roles {RolesIds}: {UsersWithRoles}, cached guild members: {DownloadedMemberCount}/{MemberCount}, quorum count: {QuorumCount}, proportion: {ConfigQuorumProportion}",
            roles.Select(role => role.Id),
            usersWithRoles,
            guild.DownloadedMemberCount,
            guild.MemberCount,
            quorumCount,
            config.QuorumProportion
        );

        return $"{quorumCount} votes are needed for quorum.";
    }
}