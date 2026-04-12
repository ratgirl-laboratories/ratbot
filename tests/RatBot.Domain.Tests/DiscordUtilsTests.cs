using ErrorOr;
using RatBot.Domain.Features.AdminSend;
using Shouldly;

namespace RatBot.Domain.Tests;

public sealed class DiscordUtilsTests
{
    [Fact]
    public void SplitMessageIntoChunks_WithExactLimit_ReturnsOneChunk()
    {
        string message = new string('a', 2000);

        string[] chunks = DiscordUtils.SplitMessageIntoChunks(message).Value;

        chunks.ShouldBe([message]);
    }

    [Fact]
    public void SplitMessageIntoChunks_WithMessageOverLimit_ReturnsTwoChunks()
    {
        string message = new string('a', 2001);

        string[] chunks = DiscordUtils.SplitMessageIntoChunks(message).Value;

        chunks.Length.ShouldBe(2);
        chunks[0].Length.ShouldBe(2000);
        chunks[1].ShouldBe("a");
    }

    [Fact]
    public void SplitMessageIntoChunks_WithNewlineBeforeLimit_SplitsAtNewline()
    {
        string firstLine = new string('a', 10);
        string secondLine = new string('b', 10);
        string message = $"{firstLine}\n{secondLine}";

        string[] chunks = DiscordUtils.SplitMessageIntoChunks(message, chunkSize: 12).Value;

        chunks.ShouldBe([$"{firstLine}\n", secondLine]);
    }

    [Fact]
    public void SplitMessageIntoChunks_WithNoNewlineBeforeLimit_SplitsAtLimit()
    {
        string message = "abcdef";

        string[] chunks = DiscordUtils.SplitMessageIntoChunks(message, chunkSize: 3).Value;

        chunks.ShouldBe(["abc", "def"]);
    }

    [Fact]
    public void SplitMessageIntoChunks_WithInvalidChunkSize_ReturnsValidationError()
    {
        ErrorOr<string[]> result = DiscordUtils.SplitMessageIntoChunks("message", chunkSize: 0);

        result.IsError.ShouldBeTrue();
        result.FirstError.Type.ShouldBe(ErrorType.Validation);
    }
}
