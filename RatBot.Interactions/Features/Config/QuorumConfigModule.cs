using JetBrains.Annotations;
using RatBot.Infrastructure.Services;
using RatBot.Interactions.Features.Quorum;

namespace RatBot.Interactions.Features.Config;

public sealed partial class ConfigModule
{
    /// <summary>
    /// Defines quorum configuration interactions.
    /// </summary>
    [Group("quorum", "Quorum configuration.")]
    public sealed class QuorumConfigModule(IConfigRepository configRepository) : SlashCommandBase
    {
        /// <summary>
        /// Creates or updates a quorum configuration for a channel or category scope.
        /// </summary>
        /// <param name="scopeId">The channel or category identifier.</param>
        /// <param name="roleIds">The comma-separated role identifiers used for quorum counting.</param>
        /// <param name="proportion">The quorum proportion in decimal form.</param>
        [SlashCommand("set", "Create or update a quorum config for a channel or category scope ID.")]
        [UsedImplicitly, RequireUserPermission(GuildPermission.Administrator)]
        public Task SetAsync(string scopeId, string roleIds, double proportion)
        {
            SetQuorumConfigArgs args = new SetQuorumConfigArgs(scopeId, roleIds, proportion);
            return ReplyAsync(args, SetResponseAsync);
        }

        private Task<string> SetResponseAsync(SetQuorumConfigArgs args) =>
            QuorumConfigs.SetForScopeAsync(configRepository, Context, args.ScopeId, args.RoleIds, args.Proportion);

        /// <summary>
        /// Removes a quorum configuration for a channel or category scope.
        /// </summary>
        /// <param name="scopeId">The channel or category identifier.</param>
        [SlashCommand("unset", "Remove a quorum config for a channel or category. The scope ID must be a channel or category ID.")]
        [RequireUserPermission(GuildPermission.Administrator)]
        [UsedImplicitly]
        public Task UnsetAsync(string scopeId)
        {
            UnsetQuorumConfigArgs args = new UnsetQuorumConfigArgs(scopeId);
            return ReplyAsync(args, UnsetResponseAsync);
        }

        private Task<string> UnsetResponseAsync(UnsetQuorumConfigArgs args) =>
            QuorumConfigs.UnsetForScopeAsync(configRepository, Context, args.ScopeId);

        private sealed record SetQuorumConfigArgs(string ScopeId, string RoleIds, double Proportion);

        private sealed record UnsetQuorumConfigArgs(string ScopeId);
    }
}
