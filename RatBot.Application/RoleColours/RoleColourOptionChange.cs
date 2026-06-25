namespace RatBot.Application.RoleColours;

public sealed record RoleColourOptionChange(RoleColourOption Option, bool Created, ulong? PreviousDisplayRoleId);
