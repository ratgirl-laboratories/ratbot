using System.Diagnostics;
using RatBot.Domain.Features.Quorum;
using QuorumCalculator = RatBot.Domain.Features.Quorum.QuorumCalculator;

namespace RatBot.Application.Features.Quorum;

public sealed class QuorumOperations(IQuorumConfigurationStore configurations, IQuorumMemberSource members)
{
    public async Task<ErrorOr<QuorumRegistration>> RegisterAsync(QuorumScope scope, decimal proportionValue, CancellationToken ct)
    {
        ErrorOr<QuorumProportion> proportion = QuorumProportion.Create(proportionValue);

        return proportion.IsError ? proportion.Errors : await configurations.RegisterAsync(scope, proportion.Value, ct);
    }

    public async Task<ErrorOr<QuorumConfiguration>> UpdateRoleAsync(QuorumRoleUpdate update, CancellationToken ct)
    {
        ErrorOr<QuorumConfiguration> configurationResult = await configurations.GetAsync(update.Scope, ct);

        if (configurationResult.IsError)
            return configurationResult.Errors;

        QuorumConfiguration updated = update switch
        {
            QuorumRoleUpdate.Add add => configurationResult.Value.AddRole(add.RoleId),
            QuorumRoleUpdate.Remove remove => configurationResult.Value.RemoveRole(remove.RoleId),
            _ => throw new UnreachableException(),
        };

        return await configurations.SaveAsync(updated, ct);
    }

    public Task<ErrorOr<Deleted>> RemoveAsync(QuorumScope scope, CancellationToken ct) => configurations.DeleteAsync(scope, ct);

    public Task<ErrorOr<QuorumConfiguration>> InspectAsync(QuorumScope scope, CancellationToken ct) => configurations.GetAsync(scope, ct);

    public async Task<ErrorOr<QuorumCalculation>> CalculateAsync(QuorumScope scope, CancellationToken ct)
    {
        ErrorOr<QuorumConfiguration> configurationResult = await configurations.GetAsync(scope, ct);

        if (configurationResult.IsError)
            return configurationResult.Errors;

        QuorumConfiguration configuration = configurationResult.Value;

        if (!configuration.IsComplete)
            return QuorumErrors.ConfigurationIncomplete;

        ErrorOr<int> eligibleVoterCount = await members.CountEligibleVotersAsync(configuration.Scope, configuration.VoterRoles.RoleIds, ct);

        if (eligibleVoterCount.IsError)
            return eligibleVoterCount.Errors;

        return QuorumCalculator.Calculate(eligibleVoterCount.Value, configuration.Proportion);
    }
}