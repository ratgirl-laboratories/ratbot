using RatBot.Application.RoleColours;
using RatBot.Infrastructure.Data;

namespace RatBot.Infrastructure.RoleColours;

public sealed class RoleColourRepository(BotDbContext db) : IRoleColourRepository
{
    public async Task<RoleColourOption[]> GetOptionsAsync(CancellationToken ct) => await db.RoleColourOptions.AsNoTracking().ToArrayAsync(ct);

    public Task<MemberColourPreference?> GetPreferenceAsync(ulong userId, CancellationToken ct) =>
        db.MemberColourPreferences.AsNoTracking().SingleOrDefaultAsync(preference => preference.UserId == userId, ct);

    public async Task SetPreferenceAsync(ulong userId, RoleColourOption.Id? optionId, CancellationToken ct)
    {
        MemberColourPreference? preference = await db.MemberColourPreferences.SingleOrDefaultAsync(preference => preference.UserId == userId, ct);

        if (preference is null)
        {
            preference = optionId is null
                ? MemberColourPreference.CreateNoColour(userId)
                : MemberColourPreference.CreateForOption(userId, optionId.Value);

            await db.MemberColourPreferences.AddAsync(preference, ct);
        }
        else if (optionId is null)
        {
            preference.SelectNoColour();
        }
        else
        {
            preference.SelectOption(optionId.Value);
        }

        await db.SaveChangesAsync(ct);
    }

    public async Task<ErrorOr<RoleColourOption>> AddOptionAsync(
        string key,
        string label,
        ulong sourceRoleId,
        ulong displayRoleId,
        CancellationToken ct
    )
    {
        ErrorOr<RoleColourOption> optionResult = RoleColourOption.Create(key, label, sourceRoleId, displayRoleId);

        if (optionResult.IsError)
            return optionResult.Errors;

        RoleColourOption option = optionResult.Value;
        RoleColourOption[] existing = await GetOptionsAsync(ct);

        if (existing.Any(existingOption => string.Equals(existingOption.NormalisedKey, option.NormalisedKey, StringComparison.Ordinal)))
            return Error.Conflict(description: $"Colour option `{option.Key}` is already registered.");

        if (existing.Any(existingOption => existingOption.SourceRoleId == sourceRoleId))
            return Error.Conflict(description: "Source role is already mapped to a colour option.");

        if (existing.Any(existingOption => existingOption.DisplayRoleId == displayRoleId))
            return Error.Conflict(description: "Display role is already mapped to a colour option.");

        await db.RoleColourOptions.AddAsync(option, ct);
        await db.SaveChangesAsync(ct);

        return option;
    }

    public async Task<ErrorOr<RoleColourOptionChange>> UpsertOptionAsync(
        string key,
        string label,
        ulong sourceRoleId,
        ulong displayRoleId,
        CancellationToken ct
    )
    {
        ErrorOr<RoleColourOption> candidateResult = RoleColourOption.Create(key, label, sourceRoleId, displayRoleId);

        if (candidateResult.IsError)
            return candidateResult.Errors;

        RoleColourOption candidate = candidateResult.Value;
        RoleColourOption[] existing = await db.RoleColourOptions.ToArrayAsync(ct);
        RoleColourOption? option = existing.SingleOrDefault(existingOption => existingOption.SourceRoleId == sourceRoleId);

        if (
            existing.Any(existingOption =>
                string.Equals(existingOption.NormalisedKey, candidate.NormalisedKey, StringComparison.Ordinal)
                && existingOption.OptionId != option?.OptionId
            )
        )
            return Error.Conflict(description: $"Colour option `{candidate.Key}` is already registered.");

        if (existing.Any(existingOption => existingOption.DisplayRoleId == displayRoleId && existingOption.OptionId != option?.OptionId))
            return Error.Conflict(description: "Display role is already mapped to a colour option.");

        if (option is null)
        {
            await db.RoleColourOptions.AddAsync(candidate, ct);
            await db.SaveChangesAsync(ct);

            return new RoleColourOptionChange(candidate, true, null);
        }

        ulong previousDisplayRoleId = option.DisplayRoleId;
        ErrorOr<Success> updateResult = option.Update(candidate.Key, candidate.Label, displayRoleId);

        if (updateResult.IsError)
            return updateResult.Errors;

        await db.SaveChangesAsync(ct);

        return new RoleColourOptionChange(option, false, previousDisplayRoleId);
    }

    public async Task<ErrorOr<RoleColourOption>> DeleteOptionAsync(string key, CancellationToken ct)
    {
        string trimmedKey = key.Trim();

        if (string.IsNullOrWhiteSpace(trimmedKey))
            return Error.Validation(description: "Key is required.");

        string normalisedKey = trimmedKey.ToUpperInvariant();
        RoleColourOption? option = await db.RoleColourOptions.SingleOrDefaultAsync(option => option.NormalisedKey == normalisedKey, ct);

        if (option is null)
            return Error.NotFound(description: $"Colour option `{trimmedKey}` is not registered.");

        MemberColourPreference[] affectedPreferences = await db
            .MemberColourPreferences.Where(preference =>
                preference.Kind == MemberColourPreferenceKind.ConfiguredOption
                && preference.SelectedOptionId != null
                && preference.SelectedOptionId.Value == option.OptionId
            )
            .ToArrayAsync(ct);

        foreach (MemberColourPreference preference in affectedPreferences)
            preference.SelectNoColour();

        db.RoleColourOptions.Remove(option);
        await db.SaveChangesAsync(ct);

        return option;
    }
}
