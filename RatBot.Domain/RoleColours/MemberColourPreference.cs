namespace RatBot.Domain.RoleColours;

public sealed class MemberColourPreference
{

    // EF Core private ctor
    private MemberColourPreference() { }

    public Id PreferenceId { get; private set; } = Id.Empty;

    public ulong UserId { get; private set; }

    public MemberColourPreferenceKind Kind { get; private set; }

    public RoleColourOption.Id? SelectedOptionId { get; private set; }

    public bool IsNoColourSelected => Kind == MemberColourPreferenceKind.NoColour;

    public static MemberColourPreference CreateForOption(
        ulong userId,
        RoleColourOption.Id selectedId) =>
        new MemberColourPreference
        {
            PreferenceId = Id.NewId(),
            UserId = userId,
            Kind = MemberColourPreferenceKind.ConfiguredOption,
            SelectedOptionId = selectedId
        };

    public static MemberColourPreference CreateNoColour(ulong userId) =>
        new MemberColourPreference
        {
            PreferenceId = Id.NewId(),
            UserId = userId,
            Kind = MemberColourPreferenceKind.NoColour,
            SelectedOptionId = null
        };

    public void SelectOption(RoleColourOption.Id id)
    {
        if (id.Equals(RoleColourOption.Id.Empty))
            throw new ArgumentException("Selected option id must be a real id.");
        Kind = MemberColourPreferenceKind.ConfiguredOption;
        SelectedOptionId = id;
    }

    public void SelectNoColour()
    {
        Kind = MemberColourPreferenceKind.NoColour;
        SelectedOptionId = null;
    }
    public readonly record struct Id(Guid Value)
    {
        public static Id Empty { get; } = new Id(Guid.Empty);

        public static Id NewId() => new Id(Guid.NewGuid());
    }
}
