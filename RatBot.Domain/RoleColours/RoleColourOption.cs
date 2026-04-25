namespace RatBot.Domain.RoleColours;

public sealed class RoleColourOption
{

    // EF Core private ctor
    private RoleColourOption() { }

    public Id OptionId { get; private set; } = Id.Empty;

    // Stable identifier used by admins and users
    public string Key { get; private set; } = null!;

    // Uppercase/normalized form for uniqueness checks
    public string NormalisedKey { get; private set; } = null!;

    // Human-friendly label for display
    public string Label { get; private set; } = null!;

    public ulong SourceRoleId { get; private set; }

    public ulong DisplayRoleId { get; private set; }

    public bool IsEnabled { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public static RoleColourOption Create(string key, string label, ulong sourceRoleId, ulong displayRoleId)
    {
        if (sourceRoleId == displayRoleId)
            throw new ArgumentException("Source and display role IDs must be different.");

        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Key is required.");

        if (string.IsNullOrWhiteSpace(label))
            throw new ArgumentException("Label is required.");

        DateTimeOffset now = DateTimeOffset.UtcNow;

        string trimmedKey = key.Trim();
        string normalized = trimmedKey.ToUpperInvariant();

        return new RoleColourOption
        {
            OptionId = Id.NewId(),
            Key = trimmedKey,
            NormalisedKey = normalized,
            Label = label.Trim(),
            SourceRoleId = sourceRoleId,
            DisplayRoleId = displayRoleId,
            IsEnabled = true,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
    }

    public void Rename(string label)
    {
        if (string.IsNullOrWhiteSpace(label))
            throw new ArgumentException("Label is required.");

        Label = label.Trim();
        Touch();
    }

    public void Enable()
    {
        if (IsEnabled)
            return;

        IsEnabled = true;
        Touch();
    }

    public void Disable()
    {
        if (!IsEnabled)
            return;

        IsEnabled = false;
        Touch();
    }

    private void Touch() => UpdatedAtUtc = DateTimeOffset.UtcNow;
    // Real SCR/DCR mappings only. The built-in "no colour" preference is not configured here.
    public readonly record struct Id(Guid Value)
    {
        public static Id Empty { get; } = new Id(Guid.Empty);
        public static Id NewId() => new Id(Guid.NewGuid());
    }
}
