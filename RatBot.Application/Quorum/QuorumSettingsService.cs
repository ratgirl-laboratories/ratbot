namespace RatBot.Application.Quorum;

public sealed class QuorumSettingsService(IQuorumSettingsRepository repository, ILogger logger)
{
    private readonly ILogger _logger = logger.ForContext<QuorumSettingsService>();

    private static ErrorOr<ValidatedQuorumSettingsInput> ValidateInput(
        ulong guildId,
        QuorumSettingsType targetType,
        ulong targetId,
        IEnumerable<ulong> roleIds,
        double quorumProportion
    )
    {
        if (!Enum.IsDefined(targetType))
            return Error.Validation(description: "Invalid quorum configuration type.");

        if (double.IsNaN(quorumProportion) || double.IsInfinity(quorumProportion))
            return Error.Validation(description: "Quorum proportion must be a finite number.");

        if (quorumProportion is <= 0 or > 1)
            return Error.Validation(description: "Quorum proportion must be greater than 0 and at most 1.");

        ulong[] validatedRoleIds = roleIds.Distinct().ToArray();

        if (validatedRoleIds.Length == 0)
            return Error.Validation(description: "At least one role must be provided.");

        return new ValidatedQuorumSettingsInput(guildId, targetType, targetId, validatedRoleIds, quorumProportion);
    }

    public async Task<ErrorOr<QuorumSettingsUpsertResult>> UpsertAsync(
        ulong guildId,
        QuorumSettingsType targetType,
        ulong targetId,
        IEnumerable<ulong> roleIds,
        double quorumProportion,
        CancellationToken ct = default
    )
    {
        ErrorOr<ValidatedQuorumSettingsInput> validationResult = ValidateInput(
            guildId,
            targetType,
            targetId,
            roleIds,
            quorumProportion);

        if (validationResult.IsError)
            return validationResult.Errors;

        ValidatedQuorumSettingsInput validatedInput = validationResult.Value;

        ErrorOr<QuorumSettings> existingResult = await repository.GetAsync(
            validatedInput.GuildId,
            validatedInput.TargetType,
            validatedInput.TargetId
        );

        bool created = existingResult.IsError;

        QuorumSettings config = new QuorumSettings(
            validatedInput.GuildId,
            validatedInput.TargetType,
            validatedInput.TargetId,
            validatedInput.QuorumProportion
        );

        config.ReplaceRoles(validatedInput.RoleIds);

        ErrorOr<Success> upsertResult = await repository.UpsertAsync(config);

        if (upsertResult.IsError)
            return upsertResult.Errors;

        _logger.Information(
            "Quorum settings {Action} for guild {GuildId}, target type {TargetType}, target {TargetId}.",
            created
                ? "created"
                : "updated",
            guildId,
            targetType,
            targetId
        );

        return new QuorumSettingsUpsertResult(created, config);
    }

    public async Task<ErrorOr<Deleted>> DeleteAsync(
        ulong guildId,
        QuorumSettingsType targetType,
        ulong targetId,
        CancellationToken ct = default)
    {
        ErrorOr<Deleted> result = await repository.DeleteAsync(guildId, targetType, targetId);

        _logger.Information(
            "Quorum settings delete attempted for guild {GuildId}, target type {TargetType}, target {TargetId}. Success={IsSuccess}",
            guildId,
            targetType,
            targetId,
            !result.IsError
        );

        return result;
    }

    public async Task<ErrorOr<QuorumSettings>> GetEffectiveAsync(
        ulong guildId,
        ulong channelId,
        ulong? categoryId,
        CancellationToken ct = default)
    {
        ErrorOr<QuorumSettings> channelConfig = await repository.GetAsync(
            guildId,
            QuorumSettingsType.Channel,
            channelId);

        if (!channelConfig.IsError)
            return channelConfig;

        return categoryId is null
            ? channelConfig
            : await repository.GetAsync(guildId, QuorumSettingsType.Category, categoryId.Value);
    }

    public Task<ErrorOr<QuorumSettings>> GetAsync(
        ulong guildId,
        QuorumSettingsType targetType,
        ulong targetId,
        CancellationToken ct = default) =>
        repository.GetAsync(guildId, targetType, targetId);

    private sealed record ValidatedQuorumSettingsInput(
        ulong GuildId,
        QuorumSettingsType TargetType,
        ulong TargetId,
        ulong[] RoleIds,
        double QuorumProportion
    );
}