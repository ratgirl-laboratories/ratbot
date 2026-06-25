namespace RatBot.Application.RoleColours;

public interface IRoleColourRepository
{
    Task<RoleColourOption[]> GetOptionsAsync(CancellationToken ct);

    Task<MemberColourPreference?> GetPreferenceAsync(ulong userId, CancellationToken ct);

    Task SetPreferenceAsync(ulong userId, RoleColourOption.Id? optionId, CancellationToken ct);

    Task<ErrorOr<RoleColourOption>> AddOptionAsync(string key, string label, ulong sourceRoleId, ulong displayRoleId, CancellationToken ct);

    Task<ErrorOr<RoleColourOptionChange>> UpsertOptionAsync(string key, string label, ulong sourceRoleId, ulong displayRoleId, CancellationToken ct);

    Task<ErrorOr<RoleColourOption>> DeleteOptionAsync(string key, CancellationToken ct);
}
