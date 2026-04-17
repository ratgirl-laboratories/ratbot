namespace RatBot.Application.Common.Discord;

public static class DiscordUtils
{
    private const int DiscordMessageCharacterLimit = 2000;

    public static ErrorOr<string[]> SplitMessageIntoChunks(string message, int chunkSize = DiscordMessageCharacterLimit)
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