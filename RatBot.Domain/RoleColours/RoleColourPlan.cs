using System.Collections.Immutable;

namespace RatBot.Domain.RoleColours;

public sealed record RoleColourPlan(ImmutableArray<ulong> RoleIdsToAdd, ImmutableArray<ulong> RoleIdsToRemove, ulong? TargetDisplayRoleId)
{
    public bool IsNoOp => RoleIdsToAdd.IsEmpty && RoleIdsToRemove.IsEmpty;
}
