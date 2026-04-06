using LanguageExt;
using RatBot.Domain.Enums;
using RatBot.Infrastructure.Services;

namespace RatBot.Interactions.Features.Quorum;

/// <summary>
/// Provides quorum-specific helpers over the shared config repository.
/// </summary>
public static class QuorumConfigs
{
    /// <summary>
    /// Gets the persisted family key for guild channel/category quorum configs.
    /// </summary>
    private const string GuildScopeFamily = "Quorum:GuildScopeConfig";

    /// <summary>
    /// Creates or updates a quorum configuration for a guild channel/category scope.
    /// </summary>
    /// <param name="configRepository">The shared config repository.</param>
    /// <param name="context">The interaction context.</param>
    /// <param name="scopeId">The channel or category identifier.</param>
    /// <param name="roleIds">The comma-separated role identifiers used for quorum counting.</param>
    /// <param name="proportion">The quorum proportion in decimal form.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The user-facing response message.</returns>
    public async static Task<string> SetForScopeAsync(
        IConfigRepository configRepository,
        SocketInteractionContext context,
        string scopeId,
        string roleIds,
        double proportion,
        CancellationToken ct = default)
    {
        ILogger logger = InteractionLoggerContext.Create(
                context,
                typeof(QuorumConfigs).FullName,
                $"{nameof(QuorumConfigs)}.{nameof(SetForScopeAsync)}"
            )
            .ForContext("command_name", "config quorum set")
            .ForContext("scope_id_input", scopeId)
            .ForContext("role_ids_input", roleIds)
            .ForContext("proportion_input", proportion);

        SocketGuild? guild = context.Guild;
        if (guild is null)
        {
            logger.Warning("quorum_config_set failed validation_failure={validation_failure}", "missing_guild");
            return "This command can only be used in a guild.";
        }

        if (!ulong.TryParse(scopeId, out ulong parsedScopeId))
        {
            logger.Warning("quorum_config_set failed validation_failure={validation_failure}", "invalid_scope_id");
            return "Invalid scope ID provided.";
        }

        Arr<ulong> parsedRoleIds = ParseRoleIds(roleIds);
        if (parsedRoleIds.Length == 0)
        {
            logger.Warning("quorum_config_set failed validation_failure={validation_failure}", "invalid_role_ids");
            return "Invalid role IDs provided. Please provide a comma-separated list of valid role IDs.";
        }

        SocketGuildChannel? scope = guild.Channels.FirstOrDefault(channel => channel.Id == parsedScopeId);
        if (scope is null)
        {
            logger.Warning(
                "quorum_config_set failed validation_failure={validation_failure} parsed_scope_id={parsed_scope_id}",
                "scope_not_found",
                parsedScopeId
            );
            return "Invalid scope ID provided.";
        }

        Arr<SocketRole> roles = [];
        foreach (ulong roleId in parsedRoleIds)
        {
            SocketRole? role = guild.GetRole(roleId);
            if (role is null)
            {
                logger.Warning(
                    "quorum_config_set failed validation_failure={validation_failure} invalid_role_id={invalid_role_id}",
                    "role_not_found",
                    roleId
                );
                return "One or more role IDs are invalid for this guild.";
            }

            roles = roles.Add(role);
        }

        QuorumScopeType? scopeType = GetScopeType(scope);
        if (scopeType is null)
        {
            logger.Warning(
                "quorum_config_set failed validation_failure={validation_failure} scope_type={scope_type}",
                "unsupported_scope_type",
                scope.ChannelType.ToString()
            );
            return "Invalid channel type for quorum config.";
        }

        QuorumScopeConfig config;
        try
        {
            config = QuorumScopeConfig.Create(
                guild.Id,
                scopeType.Value,
                scope.Id,
                new Arr<ulong>(roles.Select(role => role.Id)),
                proportion
            );
        }
        catch (ArgumentOutOfRangeException ex)
        {
            string validationFailure = GetSetValidationFailureCode(ex);
            logger.Warning(
                ex,
                "quorum_config_set failed validation_failure={validation_failure}",
                validationFailure
            );
            return GetSetValidationMessage(ex);
        }

        bool created;
        try
        {
            created = await SetAsync(configRepository, config, ct);
        }
        catch (Exception ex)
        {
            logger.Error(
                ex,
                "quorum_config_set failed failure_type={failure_type} parsed_scope_id={parsed_scope_id} scope_type={scope_type} role_count={role_count}",
                "persistence_exception",
                parsedScopeId,
                scopeType.Value.ToString(),
                roles.Length
            );
            throw;
        }

        string action = created
            ? "created"
            : "updated";

        string roleSummary = string.Join(", ", roles.Select(role => role.Mention));

        logger.Information(
            "quorum_config_set succeeded action={action} parsed_scope_id={parsed_scope_id} scope_type={scope_type} role_count={role_count} proportion={proportion}",
            action,
            parsedScopeId,
            scopeType.Value.ToString(),
            roles.Length,
            proportion
        );

        return scope switch
        {
            SocketTextChannel textChannel =>
                $"Quorum config {action} for channel {textChannel.Mention} with roles {roleSummary} and proportion {proportion}.",
            SocketCategoryChannel categoryChannel =>
                $"Quorum config {action} for category \"{categoryChannel.Name}\" with roles {roleSummary} and proportion {proportion}.",
            _ => "Invalid channel type for quorum config.",
        };
    }

    /// <summary>
    /// Removes a quorum configuration for a guild channel/category scope.
    /// </summary>
    /// <param name="configRepository">The shared config repository.</param>
    /// <param name="context">The interaction context.</param>
    /// <param name="scopeId">The channel or category identifier.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The user-facing response message.</returns>
    public async static Task<string> UnsetForScopeAsync(
        IConfigRepository configRepository,
        SocketInteractionContext context,
        string scopeId,
        CancellationToken ct = default)
    {
        ILogger logger = InteractionLoggerContext.Create(
                context,
                typeof(QuorumConfigs).FullName,
                $"{nameof(QuorumConfigs)}.{nameof(UnsetForScopeAsync)}"
            )
            .ForContext("command_name", "config quorum unset")
            .ForContext("scope_id_input", scopeId);

        SocketGuild? guild = context.Guild;
        if (guild is null)
        {
            logger.Warning("quorum_config_unset failed validation_failure={validation_failure}", "missing_guild");
            return "This command can only be used in a guild.";
        }

        if (!ulong.TryParse(scopeId, out ulong parsedScopeId))
        {
            logger.Warning("quorum_config_unset failed validation_failure={validation_failure}", "invalid_scope_id");
            return "Invalid scope ID provided. Please provide a valid ID.";
        }

        SocketGuildChannel? scope = guild.Channels.FirstOrDefault(channel => channel.Id == parsedScopeId);
        if (scope is null)
        {
            logger.Warning(
                "quorum_config_unset failed validation_failure={validation_failure} parsed_scope_id={parsed_scope_id}",
                "scope_not_found",
                parsedScopeId
            );
            return "Invalid scope ID provided.";
        }

        QuorumScopeType? scopeType = GetScopeType(scope);
        if (scopeType is null)
        {
            logger.Warning(
                "quorum_config_unset failed validation_failure={validation_failure} scope_type={scope_type}",
                "unsupported_scope_type",
                scope.ChannelType.ToString()
            );
            return "Invalid channel type for quorum config.";
        }

        bool deleted;
        try
        {
            deleted = await DeleteAsync(configRepository, guild.Id, scopeType.Value, scope.Id, ct);
        }
        catch (Exception ex)
        {
            logger.Error(
                ex,
                "quorum_config_unset failed failure_type={failure_type} parsed_scope_id={parsed_scope_id} scope_type={scope_type}",
                "persistence_exception",
                parsedScopeId,
                scopeType.Value.ToString()
            );
            throw;
        }

        if (!deleted)
        {
            logger.Warning(
                "quorum_config_unset failed failure_type={failure_type} parsed_scope_id={parsed_scope_id} scope_type={scope_type}",
                "config_not_found",
                parsedScopeId,
                scopeType.Value.ToString()
            );
            return "No quorum config exists for that scope.";
        }

        logger.Information(
            "quorum_config_unset succeeded parsed_scope_id={parsed_scope_id} scope_type={scope_type}",
            parsedScopeId,
            scopeType.Value.ToString()
        );

        return scope switch
        {
            SocketTextChannel textChannel => $"Quorum config removed for channel {textChannel.Mention}.",
            SocketCategoryChannel categoryChannel => $"Quorum config removed for category \"{categoryChannel.Name}\".",
            _ => "Invalid channel type for quorum config.",
        };
    }

    /// <summary>
    /// Gets a specific quorum configuration for a guild scope.
    /// </summary>
    /// <param name="configRepository">The shared config repository.</param>
    /// <param name="guildId">The guild identifier.</param>
    /// <param name="scopeType">The scope type.</param>
    /// <param name="scopeId">The scope identifier.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The matching config when present; otherwise <c>None</c>.</returns>
    private async static Task<Option<QuorumScopeConfig>> GetAsync(
        IConfigRepository configRepository,
        ulong guildId,
        QuorumScopeType scopeType,
        ulong scopeId,
        CancellationToken ct = default)
        => (await configRepository.GetFamilyAsync<QuorumScopeConfig>(GuildScopeFamily, ct))
            .Find(config => Matches(config, guildId, scopeType, scopeId));

    /// <summary>
    /// Gets the effective quorum configuration for a text channel, falling back to its category.
    /// </summary>
    /// <param name="configRepository">The shared config repository.</param>
    /// <param name="guildId">The guild identifier.</param>
    /// <param name="channelId">The channel identifier.</param>
    /// <param name="categoryId">The category identifier when present.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The matching config when present; otherwise <c>None</c>.</returns>
    public async static Task<Option<QuorumScopeConfig>> GetForChannelAsync(
        IConfigRepository configRepository,
        ulong guildId,
        ulong channelId,
        ulong? categoryId,
        CancellationToken ct = default)
    {
        Option<QuorumScopeConfig> config = await GetAsync(
            configRepository,
            guildId,
            QuorumScopeType.Channel,
            channelId,
            ct);

        if (config.IsNone && categoryId is { } resolvedCategoryId)
            config = await GetAsync(
                configRepository,
                guildId,
                QuorumScopeType.Category,
                resolvedCategoryId,
                ct);

        return config;
    }

    /// <summary>
    /// Creates or updates a quorum configuration.
    /// </summary>
    /// <param name="configRepository">The shared config repository.</param>
    /// <param name="config">The config to persist.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns><see langword="true" /> when a new config was created; otherwise <see langword="false" />.</returns>
    public async static Task<bool> SetAsync(IConfigRepository configRepository, QuorumScopeConfig config, CancellationToken ct = default)
    {
        Arr<QuorumScopeConfig> existingConfigs = await configRepository.GetFamilyAsync<QuorumScopeConfig>(GuildScopeFamily, ct);

        bool created = existingConfigs
            .Find(existing => Matches(existing, config.GuildId, config.ScopeType, config.ScopeId))
            .IsNone;

        Arr<QuorumScopeConfig> updatedConfigs = new Arr<QuorumScopeConfig>(
            existingConfigs.Where(existing => !Matches(existing, config.GuildId, config.ScopeType, config.ScopeId))
                .Append(config));

        await configRepository.ReplaceFamilyAsync(GuildScopeFamily, updatedConfigs, ct);
        return created;
    }

    /// <summary>
    /// Deletes a quorum configuration.
    /// </summary>
    /// <param name="configRepository">The shared config repository.</param>
    /// <param name="guildId">The guild identifier.</param>
    /// <param name="scopeType">The scope type.</param>
    /// <param name="scopeId">The scope identifier.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns><see langword="true" /> when a config was removed; otherwise <see langword="false" />.</returns>
    public async static Task<bool> DeleteAsync(
        IConfigRepository configRepository,
        ulong guildId,
        QuorumScopeType scopeType,
        ulong scopeId,
        CancellationToken ct = default)
    {
        Arr<QuorumScopeConfig> existingConfigs = await configRepository.GetFamilyAsync<QuorumScopeConfig>(GuildScopeFamily, ct);
        Arr<QuorumScopeConfig> updatedConfigs = new Arr<QuorumScopeConfig>(
            existingConfigs.Where(config => !Matches(config, guildId, scopeType, scopeId)));

        bool deleted = updatedConfigs.Length != existingConfigs.Length;

        if (!deleted)
            return false;

        await configRepository.ReplaceFamilyAsync(GuildScopeFamily, updatedConfigs, ct);

        return true;
    }

    private static QuorumScopeType? GetScopeType(SocketGuildChannel scope) =>
        scope.ChannelType switch
        {
            ChannelType.Text => QuorumScopeType.Channel,
            ChannelType.Category => QuorumScopeType.Category,
            _ => null,
        };

    private static Arr<ulong> ParseRoleIds(string roleIds)
    {
        if (string.IsNullOrWhiteSpace(roleIds))
            return Arr<ulong>.Empty;

        List<ulong> parsedRoleIds = [];

        foreach (string part in roleIds.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            if (!ulong.TryParse(part, out ulong roleId))
                return Arr<ulong>.Empty;

            parsedRoleIds.Add(roleId);
        }

        return new Arr<ulong>(parsedRoleIds.Distinct());
    }

    private static string GetSetValidationFailureCode(ArgumentOutOfRangeException ex) =>
        ex.ParamName switch
        {
            "value" => "invalid_proportion",
            "roleIds" => "invalid_role_ids",
            "scopeType" => "unsupported_scope_type",
            "guildId" => "invalid_guild_id",
            "scopeId" => "invalid_scope_id",
            _ => "invalid_quorum_config",
        };

    private static string GetSetValidationMessage(ArgumentOutOfRangeException ex) =>
        ex.ParamName switch
        {
            "value" => "Invalid proportion provided. Please provide a value greater than 0 and at most 1.",
            "roleIds" => "Invalid role IDs provided. Please provide a comma-separated list of valid role IDs.",
            "scopeType" => "Invalid channel type for quorum config.",
            "scopeId" => "Invalid scope ID provided.",
            _ => "Invalid quorum configuration provided.",
        };

    private static bool Matches(
        QuorumScopeConfig config,
        ulong guildId,
        QuorumScopeType scopeType,
        ulong scopeId)
        => config.GuildId == guildId && config.ScopeType == scopeType && config.ScopeId == scopeId;
}
