using ErrorOr;
using NSubstitute;
using RatBot.Application.Features.EmojiYoink;
using Serilog;
using Shouldly;

namespace RatBot.Application.Tests.Features.EmojiYoink;

public sealed class EmojiYoinkOperationsTests
{
    private const ulong GuildId = 123;
    private const ulong ModeratorUserId = 456;

    [TestCase("rat", "rat")]
    [TestCase("rat~1", "rat")]
    [TestCase("rat~27", "rat")]
    public void EmojiName_normalises_terminal_disambiguation_suffix(string invokedName, string expected)
    {
        ErrorOr<EmojiName> result = EmojiName.Create(invokedName);

        result.IsError.ShouldBeFalse();
        result.Value.Value.ShouldBe(expected);
    }

    [Test]
    public async Task YoinkAsync_rejects_invalid_normalised_name_without_calling_importer()
    {
        IGuildEmojiImporter importer = Substitute.For<IGuildEmojiImporter>();
        EmojiYoinkOperations operations = CreateOperations(importer);
        YoinkEmojiSource source = new YoinkEmojiSource(789, "a~1", false);

        ErrorOr<CreatedGuildEmoji> result = await operations.YoinkAsync(CreateCommand(source));

        result.IsError.ShouldBeTrue();
        result.FirstError.ShouldBe(EmojiYoinkErrors.InvalidDestinationName);
        await importer.DidNotReceiveWithAnyArgs().ImportAsync(default, default, default!, default, default);
    }

    [Test]
    public async Task YoinkAsync_passes_normalised_name_and_source_to_importer_and_returns_created_emoji()
    {
        IGuildEmojiImporter importer = Substitute.For<IGuildEmojiImporter>();
        EmojiYoinkOperations operations = CreateOperations(importer);
        YoinkEmojiSource source = new YoinkEmojiSource(789, "rat~27", true);
        CreatedGuildEmoji created = new CreatedGuildEmoji(987, "rat", true);
        importer
            .ImportAsync(GuildId, ModeratorUserId, Arg.Is<EmojiName>(name => name.Value == "rat"), source, Arg.Any<CancellationToken>())
            .Returns(created);

        ErrorOr<CreatedGuildEmoji> result = await operations.YoinkAsync(CreateCommand(source));

        result.IsError.ShouldBeFalse();
        result.Value.ShouldBe(created);
        await importer
            .Received(1)
            .ImportAsync(GuildId, ModeratorUserId, Arg.Is<EmojiName>(name => name.Value == "rat"), source, Arg.Any<CancellationToken>());
    }

    private static EmojiYoinkOperations CreateOperations(IGuildEmojiImporter importer) =>
        new EmojiYoinkOperations(importer, new LoggerConfiguration().CreateLogger());

    private static YoinkEmojiCommand CreateCommand(YoinkEmojiSource source) => new YoinkEmojiCommand(GuildId, ModeratorUserId, "moderator", source);
}
