using RatBot.Application.RoleColours;
using RatBot.Infrastructure.Data;

namespace RatBot.Infrastructure.RoleColours;

public static class RegisterRoleColourOption
{

    public async static Task<ErrorOr<RoleColourOption>> ExecuteAsync(
        BotDbContext db,
        Command command,
        CancellationToken ct)
    {
        string key = command.Key.Trim();
        string label = command.Label.Trim();

        if (string.IsNullOrWhiteSpace(key))
            return Error.Validation(description: "Key is required.");

        if (string.IsNullOrWhiteSpace(label))
            return Error.Validation(description: "Label is required.");

        if (command.SourceRoleId == command.DisplayRoleId)
            return Error.Validation(description: "Source and display roles must be different.");

        RoleColourRegistrationContext ctx = command.RegistrationContext;

        if (!ctx.SourceRoleExists)
            return Error.Validation(description: "Source role does not exist in this guild.");

        if (!ctx.DisplayRoleExists)
            return Error.Validation(description: "Display role does not exist in this guild.");

        if (ctx.SourceRoleHasColour)
            return Error.Validation(description: "Source role must not have a colour set. Clear the colour first.");

        string normalizedKey = key.ToUpperInvariant();

        List<RoleColourOption> existing = await db.RoleColourOptions.AsNoTracking().ToListAsync(ct);

        if (existing.Any(o => o.NormalisedKey == normalizedKey))
            return Error.Conflict(description: $"Colour option `{key}` is already registered.");

        if (existing.Any(o => o.SourceRoleId == command.SourceRoleId))
            return Error.Conflict(description: "Source role is already mapped to a colour option.");

        if (existing.Any(o => o.DisplayRoleId == command.DisplayRoleId))
            return Error.Conflict(description: "Display role is already mapped to a colour option.");

        RoleColourOption option = RoleColourOption.Create(
            key,
            label,
            command.SourceRoleId,
            command.DisplayRoleId);

        await db.RoleColourOptions.AddAsync(option, ct);
        await db.SaveChangesAsync(ct);

        return option;
    }
    public sealed record Command(
        string Key,
        string Label,
        ulong SourceRoleId,
        ulong DisplayRoleId,
        RoleColourRegistrationContext RegistrationContext
    );
}
