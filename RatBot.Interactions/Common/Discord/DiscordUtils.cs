namespace RatBot.Interactions.Common.Discord;

/// <summary>
///     A collection of utility methods for Discord interactions.
/// </summary>
public static class DiscordUtils
{
    /// <summary>
    ///     Discord's current maximum number of characters per message.
    /// </summary>
    private const int DiscordMessageCharacterLimit = 2000;

    /// <summary>
    ///     Splits a message into chunks no longer than <paramref name="chunkSize" />.
    ///     Prefers newline boundaries when possible.
    /// </summary>
    public static ErrorOr<string[]> SplitMessageIntoChunks(
        string message,
        int chunkSize = DiscordMessageCharacterLimit)
    {
        if (chunkSize <= 0)
            return Error.Validation("Chunk size must be greater than zero.");

        Queue<string> chunks = new Queue<string>(message.Length / chunkSize + 1);

        int index = 0;

        while (index < message.Length)
        {
            int remainingLength = message.Length - index;

            if (remainingLength <= chunkSize)
            {
                chunks.Enqueue(message[index..]);
                break;
            }

            string window = message.Substring(index, chunkSize);
            int splitAt = window.LastIndexOf('\n');

            int chunkLength = splitAt > 0
                ? splitAt + 1
                : chunkSize;

            chunks.Enqueue(message.Substring(index, chunkLength));
            index += chunkLength;
        }

        return chunks.ToArray();
    }
}