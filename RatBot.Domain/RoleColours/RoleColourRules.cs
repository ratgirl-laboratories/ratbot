using System.Collections.Immutable;

namespace RatBot.Domain.RoleColours;

public static class RoleColourRules
{
    public static RoleColourPlan CreatePlan(
        IReadOnlyCollection<RoleColourOption> options,
        MemberColourPreference? preference,
        IReadOnlyCollection<ulong> currentRoleIds,
        IReadOnlyDictionary<ulong, int> sourceRolePositions
    )
    {
        RoleColourOption[] enabledOptions = options.Where(option => option.IsEnabled).ToArray();

        ulong? targetDisplayRoleId =
            preference?.IsNoColourSelected == true
                ? null
                : ResolveConfiguredTarget(preference, enabledOptions, currentRoleIds)
                    ?? ResolveFallbackTarget(enabledOptions, currentRoleIds, sourceRolePositions);

        HashSet<ulong> managedDisplayRoleIds = options.Select(option => option.DisplayRoleId).ToHashSet();

        ImmutableArray<ulong> roleIdsToRemove = currentRoleIds
            .Where(roleId => managedDisplayRoleIds.Contains(roleId) && roleId != targetDisplayRoleId)
            .ToImmutableArray();

        ImmutableArray<ulong> roleIdsToAdd =
            targetDisplayRoleId is { } target && !currentRoleIds.Contains(target) ? [target] : ImmutableArray<ulong>.Empty;

        return new RoleColourPlan(roleIdsToAdd, roleIdsToRemove, targetDisplayRoleId);
    }

    private static ulong? ResolveConfiguredTarget(
        MemberColourPreference? preference,
        IReadOnlyCollection<RoleColourOption> enabledOptions,
        IReadOnlyCollection<ulong> currentRoleIds
    )
    {
        if (preference is not { Kind: MemberColourPreferenceKind.ConfiguredOption, SelectedOptionId: not null })
            return null;

        RoleColourOption? selectedOption = enabledOptions.SingleOrDefault(option => option.OptionId == preference.SelectedOptionId.Value);

        return selectedOption is not null && currentRoleIds.Contains(selectedOption.SourceRoleId) ? selectedOption.DisplayRoleId : null;
    }

    private static ulong? ResolveFallbackTarget(
        IEnumerable<RoleColourOption> enabledOptions,
        IReadOnlyCollection<ulong> currentRoleIds,
        IReadOnlyDictionary<ulong, int> sourceRolePositions
    ) =>
        enabledOptions
            .Where(option => currentRoleIds.Contains(option.SourceRoleId))
            .OrderByDescending(option => sourceRolePositions.GetValueOrDefault(option.SourceRoleId, int.MinValue))
            .ThenBy(option => option.Label, StringComparer.OrdinalIgnoreCase)
            .Select(option => (ulong?)option.DisplayRoleId)
            .FirstOrDefault();
}
