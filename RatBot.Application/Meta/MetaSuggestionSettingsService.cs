using RatBot.Application.Common;

namespace RatBot.Application.Meta;

public sealed class MetaSuggestionSettingsService(IUnitOfWork uow, ILogger logger)
{
    private readonly ILogger _logger = logger.ForContext<MetaSuggestionSettingsService>();

    public async Task<ErrorOr<MetaSuggestionSettings>> GetAsync(ulong guildId, CancellationToken ct = default)
    {
        _ = ct;
        IRepository<MetaSuggestionSettings> settings = uow.GetRepository<MetaSuggestionSettings>();
        ErrorOr<MetaSuggestionSettings> setting = await settings.TryFindAsync((long)guildId);
        return setting.IsError
            ? MetaProposalErrors.SettingsNotConfigured
            : setting.Value;
    }

    public Task<ErrorOr<Success>> UpsertSuggestionsForumChannelAsync(
        ulong guildId,
        ulong forumChannelId,
        CancellationToken ct = default) =>
        UpsertAsync(guildId, settings => settings.SetSuggestionsForum(forumChannelId), ct);

    public Task<ErrorOr<Success>> UpsertProposalsForumChannelAsync(
        ulong guildId,
        ulong forumChannelId,
        CancellationToken ct = default) =>
        UpsertAsync(guildId, settings => settings.SetProposalsForum(forumChannelId), ct);

    public Task<ErrorOr<Success>> UpsertCabinetRoleAsync(
        ulong guildId,
        ulong roleId,
        CancellationToken ct = default) =>
        UpsertAsync(guildId, settings => settings.SetCabinetRole(roleId), ct);

    public Task<ErrorOr<Success>> UpsertCabinetChairRoleAsync(
        ulong guildId,
        ulong roleId,
        CancellationToken ct = default) =>
        UpsertAsync(guildId, settings => settings.SetCabinetChairRole(roleId), ct);

    public Task<ErrorOr<Success>> UpsertCommitteeRoleAsync(
        ulong guildId,
        ulong roleId,
        CancellationToken ct = default) =>
        UpsertAsync(guildId, settings => settings.SetCommitteeRole(roleId), ct);

    private async Task<ErrorOr<Success>> UpsertAsync(
        ulong guildId,
        Func<MetaSuggestionSettings, ErrorOr<Success>> update,
        CancellationToken ct)
    {
        IRepository<MetaSuggestionSettings> repo = uow.GetRepository<MetaSuggestionSettings>();
        ErrorOr<MetaSuggestionSettings> existing = await repo.TryFindAsync((long)guildId);

        MetaSuggestionSettings settings;

        if (existing.IsError)
        {
            settings = MetaSuggestionSettings.Create(guildId);
            repo.Add(settings);
        }
        else
        {
            settings = existing.Value;
        }

        ErrorOr<Success> result = update(settings);

        if (result.IsError)
            return result.Errors;

        await uow.SaveChangesAsync(ct);

        _logger.Information("Meta proposal settings updated for guild {GuildId}.", guildId);

        return Result.Success;
    }
}
