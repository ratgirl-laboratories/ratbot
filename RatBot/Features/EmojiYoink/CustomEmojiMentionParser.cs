using RatBot.Application.Features.EmojiYoink;

namespace RatBot.Features.EmojiYoink;

public static class CustomEmojiMentionParser
{
    public static ErrorOr<YoinkEmojiSource> Parse(string input)
    {
        string candidate = input.Trim();

        if (!Emote.TryParse(candidate, out Emote? emote) || emote.Id == 0 || !string.Equals(candidate, emote.ToString(), StringComparison.Ordinal))
        {
            return EmojiYoinkErrors.InvalidCustomEmoji;
        }

        return new YoinkEmojiSource(emote.Id, emote.Name, emote.Animated);
    }
}
