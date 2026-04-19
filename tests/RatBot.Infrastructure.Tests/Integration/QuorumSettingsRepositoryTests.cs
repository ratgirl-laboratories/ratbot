using RatBot.Domain.Quorum;
using RatBot.Infrastructure.Data;
using RatBot.Infrastructure.Persistence.Repositories;

namespace RatBot.Infrastructure.Tests.Integration;

[TestFixture]
public sealed class QuorumSettingsRepositoryTests
{
    private BotDbContext _db = null!;
    private QuorumSettingsRepository _repository = null!;

    [SetUp]
    public async Task SetUp()
    {
        await PostgresDatabaseFixture.ResetAsync();
        _db = PostgresDatabaseFixture.CreateDbContext();
        _repository = new QuorumSettingsRepository(_db);
    }

    [TearDown]
    public async Task TearDown()
    {
        await _db.DisposeAsync();
    }

    [Test]
    public async Task GetAsync_ShouldReturnNotFound_WhenMissing()
    {
        QuorumTarget target = QuorumTarget.Create(1, QuorumSettingsType.Channel, 123).Value;

        ErrorOr<QuorumSettings> result = await _repository.GetAsync(target);

        result.IsError.ShouldBeTrue();
        result.FirstError.Type.ShouldBe(ErrorType.NotFound);
    }

    [Test]
    public async Task UpsertAsync_ShouldInsertNewConfiguration()
    {
        QuorumTarget target = QuorumTarget.Create(1, QuorumSettingsType.Channel, 123).Value;
        Proportion proportion = Proportion.Create(0.5).Value;
        QuorumSettings settings = QuorumSettings.Create(target, [10UL, 20UL], proportion).Value;

        ErrorOr<Success> saveResult = await _repository.UpsertAsync(settings);
        saveResult.IsError.ShouldBeFalse();

        await _db.Entry(settings).ReloadAsync();

        ErrorOr<QuorumSettings> loadResult = await _repository.GetAsync(target);
        loadResult.IsError.ShouldBeFalse();
        loadResult.Value.Proportion.ShouldBe(0.5);
        loadResult.Value.Roles.Select(x => x.Id).Order().ShouldBe([10UL, 20UL]);
    }

    [Test]
    public async Task UpsertAsync_ShouldReplaceExistingRoles()
    {
        QuorumTarget target = QuorumTarget.Create(1, QuorumSettingsType.Channel, 123).Value;
        Proportion initial = Proportion.Create(0.5).Value;
        Proportion updated = Proportion.Create(0.75).Value;

        QuorumSettings first = QuorumSettings.Create(target, [10UL, 20UL], initial).Value;
        await _repository.UpsertAsync(first);

        QuorumSettings existing = (await _repository.GetAsync(target)).Value;
        existing.Update([30UL], updated).IsError.ShouldBeFalse();

        ErrorOr<Success> saveResult = await _repository.UpsertAsync(existing);
        saveResult.IsError.ShouldBeFalse();

        ErrorOr<QuorumSettings> loadResult = await _repository.GetAsync(target);
        loadResult.IsError.ShouldBeFalse();
        loadResult.Value.Proportion.ShouldBe(0.75);
        loadResult.Value.Roles.Select(x => x.Id).ShouldBe([30UL]);
    }

    [Test]
    public async Task DeleteAsync_ShouldRemoveConfiguration()
    {
        QuorumTarget target = QuorumTarget.Create(1, QuorumSettingsType.Channel, 123).Value;
        Proportion proportion = Proportion.Create(0.5).Value;
        QuorumSettings settings = QuorumSettings.Create(target, [10UL], proportion).Value;

        await _repository.UpsertAsync(settings);

        ErrorOr<Deleted> deleteResult = await _repository.DeleteAsync(target);
        deleteResult.IsError.ShouldBeFalse();

        ErrorOr<QuorumSettings> loadResult = await _repository.GetAsync(target);
        loadResult.IsError.ShouldBeTrue();
        loadResult.FirstError.Type.ShouldBe(ErrorType.NotFound);
    }
}