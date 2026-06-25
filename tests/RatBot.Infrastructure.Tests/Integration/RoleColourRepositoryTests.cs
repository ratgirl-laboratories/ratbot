using RatBot.Domain.RoleColours;
using RatBot.Infrastructure.Data;
using RatBot.Infrastructure.RoleColours;

namespace RatBot.Infrastructure.Tests.Integration;

[TestFixture]
public sealed class RoleColourRepositoryTests
{
    private BotDbContext _db = null!;
    private RoleColourRepository _repository = null!;

    [SetUp]
    public async Task SetUp()
    {
        await PostgresDatabaseFixture.ResetAsync();
        _db = PostgresDatabaseFixture.CreateDbContext();
        _repository = new RoleColourRepository(_db);
    }

    [TearDown]
    public async Task TearDown() => await _db.DisposeAsync();

    [Test]
    public async Task GetPreferenceAsync_ShouldReturnUnspecified_WhenMissing()
    {
        MemberColourPreference preference = await _repository.GetPreferenceAsync(123, CancellationToken.None);

        preference.UserId.ShouldBe(123UL);
        preference.Kind.ShouldBe(MemberColourPreferenceKind.Unspecified);
        preference.SelectedOptionId.ShouldBeNull();
    }

    [Test]
    public async Task SetPreferenceAsync_ShouldUpdateExistingPreference()
    {
        ErrorOr<RoleColourOption> optionResult = RoleColourOption.Create("red", "Red", 10, 20);
        optionResult.IsError.ShouldBeFalse();
        RoleColourOption option = optionResult.Value;
        await _db.RoleColourOptions.AddAsync(option);
        await _db.MemberColourPreferences.AddAsync(MemberColourPreference.CreateNoColour(123));
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();

        await _repository.SetPreferenceAsync(123, option.OptionId, CancellationToken.None);
        _db.ChangeTracker.Clear();

        MemberColourPreference preference = await _repository.GetPreferenceAsync(123, CancellationToken.None);

        preference.Kind.ShouldBe(MemberColourPreferenceKind.ConfiguredOption);
        preference.SelectedOptionId.ShouldBe(option.OptionId);
    }
}
