using RatBot.Domain.RoleColours;
using Shouldly;

namespace RatBot.Domain.Tests.RoleColours;

[TestFixture]
public sealed class MemberColourPreferenceTests
{

    private static RoleColourOption CreateRoleColourOption() =>
        RoleColourOption.Create("red", "Red", 10, 20).Value;
    [Test]
    public void CreateForOption_StoresConfiguredOptionSelection()
    {
        RoleColourOption option = CreateRoleColourOption();

        MemberColourPreference preference = MemberColourPreference.CreateForOption(100, option.OptionId);

        preference.UserId.ShouldBe(100UL);
        preference.Kind.ShouldBe(MemberColourPreferenceKind.ConfiguredOption);
        preference.SelectedOptionId.ShouldBe(option.OptionId);
        preference.IsNoColourSelected.ShouldBeFalse();
    }

    [Test]
    public void CreateNoColour_StoresBuiltInNoColourSelectionWithoutConfiguredOption()
    {
        MemberColourPreference preference = MemberColourPreference.CreateNoColour(100);

        preference.Kind.ShouldBe(MemberColourPreferenceKind.NoColour);
        preference.SelectedOptionId.ShouldBeNull();
        preference.IsNoColourSelected.ShouldBeTrue();
    }

    [Test]
    public void SelectNoColour_ClearsConfiguredOptionSelection()
    {
        RoleColourOption option = CreateRoleColourOption();
        MemberColourPreference preference = MemberColourPreference.CreateForOption(100, option.OptionId);

        preference.SelectNoColour();

        preference.Kind.ShouldBe(MemberColourPreferenceKind.NoColour);
        preference.SelectedOptionId.ShouldBeNull();
        preference.IsNoColourSelected.ShouldBeTrue();
    }

    [Test]
    public void SelectOption_ReplacesNoColourSelection()
    {
        RoleColourOption option = CreateRoleColourOption();
        MemberColourPreference preference = MemberColourPreference.CreateNoColour(100);

        preference.SelectOption(option.OptionId);

        preference.Kind.ShouldBe(MemberColourPreferenceKind.ConfiguredOption);
        preference.SelectedOptionId.ShouldBe(option.OptionId);
        preference.IsNoColourSelected.ShouldBeFalse();
    }
}
