using ErrorOr;
using RatBot.Application.Features.Timezone;
using Shouldly;

namespace RatBot.Application.Tests.Features.Timezone;

public sealed class IanaTimezoneIdTests
{
    [TestCase("UTC")]
    [TestCase("Europe/London")]
    [TestCase("America/New_York")]
    public void Create_accepts_known_timezone_ids(string input)
    {
        ErrorOr<IanaTimezoneId> result = IanaTimezoneId.Create(input);

        result.IsError.ShouldBeFalse();
        result.Value.Value.ShouldBe(input);
    }

    [Test]
    public void Create_trims_surrounding_whitespace()
    {
        ErrorOr<IanaTimezoneId> result = IanaTimezoneId.Create("  Europe/London  ");

        result.IsError.ShouldBeFalse();
        result.Value.Value.ShouldBe("Europe/London");
    }

    [TestCase("utc", "UTC")]
    [TestCase("Utc", "UTC")]
    [TestCase("europe/london", "Europe/London")]
    [TestCase("america/new_york", "America/New_York")]
    public void Create_canonicalises_casing_to_system_timezone_id(string input, string expected)
    {
        ErrorOr<IanaTimezoneId> result = IanaTimezoneId.Create(input);

        result.IsError.ShouldBeFalse();
        result.Value.Value.ShouldBe(expected);
    }

    [TestCase("")]
    [TestCase(" ")]
    [TestCase("BST")]
    [TestCase("EST")]
    [TestCase("+01:00")]
    [TestCase("GMT Standard Time")]
    [TestCase("London")]
    public void Create_rejects_invalid_timezone_input(string input)
    {
        ErrorOr<IanaTimezoneId> result = IanaTimezoneId.Create(input);

        result.IsError.ShouldBeTrue();
        result.FirstError.Code.ShouldBe(TimezoneErrors.InvalidInput.Code);
    }

    [Test]
    public void ToString_returns_stored_timezone_id()
    {
        IanaTimezoneId timezone = IanaTimezoneId.Create("Europe/London").Value;

        timezone.ToString().ShouldBe("Europe/London");
    }

    [Test]
    public void Resolve_returns_matching_timezone_info()
    {
        IanaTimezoneId timezone = IanaTimezoneId.Create("Europe/London").Value;
        TimeZoneInfo resolved = timezone.Resolve();

        resolved.Id.ShouldBe("Europe/London");
    }
}
