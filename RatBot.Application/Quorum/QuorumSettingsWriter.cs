namespace RatBot.Application.Quorum;

public sealed class QuorumSettingsWriter(
    IQuorumSettingsRepository repository,
    ILogger logger) : IQuorumSettingsWriter
{
    private readonly ILogger _logger = logger.ForContext<QuorumSettingsWriter>();

    public async Task<ErrorOr<QuorumSettingsUpsertResult>> UpsertAsync(
        QuorumTarget target,
        IEnumerable<ulong> roleIds,
        double quorumProportion,
        CancellationToken ct = default)
    {
        _ = ct;

        ErrorOr<Proportion> quorumProportionResult = Proportion.Create(quorumProportion);

        if (quorumProportionResult.IsError)
            return quorumProportionResult.Errors;

        Proportion validatedProportion = quorumProportionResult.Value;
        ErrorOr<QuorumSettings> existingResult = await repository.GetAsync(target);

        bool created;
        QuorumSettings config;

        if (existingResult.IsError)
        {
            if (existingResult.Errors.Any(error => error.Type != ErrorType.NotFound))
                return existingResult.Errors;

            ErrorOr<QuorumSettings> createResult =
                QuorumSettings.Create(target, roleIds, validatedProportion);

            if (createResult.IsError)
                return createResult.Errors;

            created = true;
            config = createResult.Value;
        }
        else
        {
            created = false;
            config = existingResult.Value;

            ErrorOr<Success> changeResult = config.Update(roleIds, validatedProportion);

            if (changeResult.IsError)
                return changeResult.Errors;
        }

        ErrorOr<Success> upsertResult = await repository.UpsertAsync(config);

        if (upsertResult.IsError)
            return upsertResult.Errors;

        _logger.Information(
            "Quorum settings {Action} for guild {GuildId}, target type {TargetType}, target {TargetId}.",
            created
                ? "created"
                : "updated",
            target.GuildId,
            target.TargetType,
            target.TargetId
        );

        return new QuorumSettingsUpsertResult(created, config);
    }

    public async Task<ErrorOr<Deleted>> DeleteAsync(
        QuorumTarget target,
        CancellationToken ct = default)
    {
        _ = ct;

        ErrorOr<Deleted> result = await repository.DeleteAsync(target);

        _logger.Information(
            "Quorum settings delete attempted for guild {GuildId}, target type {TargetType}, target {TargetId}. Success={IsSuccess}",
            target.GuildId,
            target.TargetType,
            target.TargetId,
            !result.IsError
        );

        return result;
    }
}