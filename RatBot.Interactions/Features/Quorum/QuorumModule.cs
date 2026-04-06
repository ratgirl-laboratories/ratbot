using RatBot.Infrastructure.Services;

namespace RatBot.Interactions.Features.Quorum;

/// <summary>
/// Defines quorum-related interactions.
/// </summary>
[Group("quorum", "Quorum commands. Group restricted to moderators by default.")]
[DefaultMemberPermissions(GuildPermission.MuteMembers)]
public sealed partial class QuorumModule(ILogger logger, IConfigRepository configRepository) : SlashCommandBase;