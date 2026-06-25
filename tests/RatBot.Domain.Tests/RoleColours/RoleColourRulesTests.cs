using RatBot.Domain.RoleColours;
using Shouldly;

namespace RatBot.Domain.Tests.RoleColours;

[TestFixture]
public sealed class RoleColourRulesTests
{
    [Test]
    public void ConfiguredPreferenceSelected()
    {
        RoleColourOption preferred = CreateOption("red", "Red", 10, 20);
        RoleColourOption fallback = CreateOption("blue", "Blue", 11, 21);
        MemberColourPreference preference = MemberColourPreference.CreateForOption(100, preferred.OptionId);

        RoleColourPlan plan = RoleColourRules.CreatePlan(
            [preferred, fallback],
            preference,
            [10, 11, 21],
            new Dictionary<ulong, int> { [10] = 1, [11] = 100 }
        );

        plan.TargetDisplayRoleId.ShouldBe(20UL);
        plan.RoleIdsToAdd.ShouldBe([20UL]);
        plan.RoleIdsToRemove.ShouldBe([21UL]);
    }

    [Test]
    public void NoColourRemovesDisplayRoles()
    {
        RoleColourOption red = CreateOption("red", "Red", 10, 20);
        RoleColourOption blue = CreateOption("blue", "Blue", 11, 21);
        MemberColourPreference preference = MemberColourPreference.CreateNoColour(100);

        RoleColourPlan plan = RoleColourRules.CreatePlan([red, blue], preference, [10, 20, 21], new Dictionary<ulong, int>());

        plan.TargetDisplayRoleId.ShouldBeNull();
        plan.RoleIdsToAdd.ShouldBeEmpty();
        plan.RoleIdsToRemove.ShouldBe([20UL, 21UL]);
    }

    [Test]
    public void FallbackUsesHighestSourceRolePosition()
    {
        RoleColourOption red = CreateOption("red", "Red", 10, 20);
        RoleColourOption blue = CreateOption("blue", "Blue", 11, 21);

        RoleColourPlan plan = RoleColourRules.CreatePlan([red, blue], null, [10, 11], new Dictionary<ulong, int> { [10] = 5, [11] = 10 });

        plan.TargetDisplayRoleId.ShouldBe(21UL);
        plan.RoleIdsToAdd.ShouldBe([21UL]);
    }

    [Test]
    public void FallbackTiesByLabel()
    {
        RoleColourOption red = CreateOption("red", "Zulu", 10, 20);
        RoleColourOption blue = CreateOption("blue", "Alpha", 11, 21);

        RoleColourPlan plan = RoleColourRules.CreatePlan([red, blue], null, [10, 11], new Dictionary<ulong, int> { [10] = 5, [11] = 5 });

        plan.TargetDisplayRoleId.ShouldBe(21UL);
    }

    [Test]
    public void DisabledPreferredOptionIsRemovedAndNotSelected()
    {
        RoleColourOption disabled = CreateOption("red", "Red", 10, 20);
        disabled.Disable();
        RoleColourOption fallback = CreateOption("blue", "Blue", 11, 21);
        MemberColourPreference preference = MemberColourPreference.CreateForOption(100, disabled.OptionId);

        RoleColourPlan plan = RoleColourRules.CreatePlan(
            [disabled, fallback],
            preference,
            [10, 11, 20],
            new Dictionary<ulong, int> { [10] = 100, [11] = 1 }
        );

        plan.TargetDisplayRoleId.ShouldBe(21UL);
        plan.RoleIdsToAdd.ShouldBe([21UL]);
        plan.RoleIdsToRemove.ShouldBe([20UL]);
    }

    [Test]
    public void AlreadyCorrectIsNoOp()
    {
        RoleColourOption preferred = CreateOption("red", "Red", 10, 20);
        MemberColourPreference preference = MemberColourPreference.CreateForOption(100, preferred.OptionId);

        RoleColourPlan plan = RoleColourRules.CreatePlan([preferred], preference, [10, 20], new Dictionary<ulong, int> { [10] = 1 });

        plan.TargetDisplayRoleId.ShouldBe(20UL);
        plan.IsNoOp.ShouldBeTrue();
    }

    private static RoleColourOption CreateOption(string key, string label, ulong sourceRoleId, ulong displayRoleId) =>
        RoleColourOption.Create(key, label, sourceRoleId, displayRoleId).Value;
}
