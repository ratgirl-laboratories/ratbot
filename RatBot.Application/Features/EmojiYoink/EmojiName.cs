namespace RatBot.Application.Features.EmojiYoink;

public sealed record EmojiName
{
    private EmojiName(string value)
    {
        Value = value;
    }

    public string Value { get; }

    public static ErrorOr<EmojiName> Create(string invokedSourceName)
    {
        string destinationName = RemoveTerminalDisambiguationSuffix(invokedSourceName);

        return destinationName.Length is >= 2 and <= 32 && destinationName.All(IsValidCharacter)
            ? new EmojiName(destinationName)
            : EmojiYoinkErrors.InvalidDestinationName;
    }

    public override string ToString() => Value;

    private static string RemoveTerminalDisambiguationSuffix(string invokedSourceName)
    {
        int suffixStart = invokedSourceName.LastIndexOf('~');

        if (
            suffixStart < 0
            || suffixStart == invokedSourceName.Length - 1
            || invokedSourceName[suffixStart + 1] is < '1' or > '9'
            || !invokedSourceName[(suffixStart + 2)..].All(character => character is >= '0' and <= '9')
        )
        {
            return invokedSourceName;
        }

        return invokedSourceName[..suffixStart];
    }

    private static bool IsValidCharacter(char character) => character is >= 'A' and <= 'Z' or >= 'a' and <= 'z' or >= '0' and <= '9' or '_';
}
