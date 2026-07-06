using Discord;
using NSubstitute;
using RatBot.Application.Features.Logging;
using RatBot.Discord.Features.Logging.Gateway;
using Shouldly;

namespace RatBot.Discord.Tests.Features.Logging;

public sealed class ModerationLogComponentsTests
{
    [Test]
    public void GetJumpUrl_UsesDiscordNetMessageJumpUrl()
    {
        ITextChannel channel = Substitute.For<ITextChannel>();
        IMessage message = Substitute.For<IMessage>();

        channel.GuildId.Returns(123UL);
        channel.Id.Returns(456UL);
        message.Channel.Returns(channel);
        message.Id.Returns(789UL);

        ModerationLogComponents.GetJumpUrl(message).ShouldBe("https://discord.com/channels/123/456/789");
    }

    [Test]
    public void BuildEditLog_RendersBeforeAndAfterUnavailableStates()
    {
        ModerationLogComponents.ModerationLogMessage log = ModerationLogComponents.BuildEditLog(
            "https://discord.com/channels/1/2/3",
            4,
            new DateTimeOffset(2026, 7, 6, 12, 34, 56, TimeSpan.FromHours(1)),
            null,
            null,
            Array.Empty<CachedAttachmentEvidence>()
        );

        string[] text = GetTextContent(log);

        text.ShouldContain("### Content");
        text.ShouldContain("```ansi\nBefore: *Unavailable*\nAfter:  *Unavailable*\n```");
        text.Single(value => value.Contains("**Edited**", StringComparison.Ordinal)).ShouldContain("2026-07-06 11:34:56Z");
    }

    [Test]
    public void BuildEditLog_RendersMessageDiffAsAnsiBlock()
    {
        ModerationLogComponents.ModerationLogMessage log = ModerationLogComponents.BuildEditLog(
            "https://discord.com/channels/1/2/3",
            4,
            DateTimeOffset.UtcNow,
            "Hayes",
            "hates",
            Array.Empty<CachedAttachmentEvidence>()
        );

        string content = GetTextContent(log).Single(value => value.StartsWith("```ansi", StringComparison.Ordinal));

        content.ShouldBe("```ansi\n" + "Before: \e[31mH\e[0ma\e[31my\e[0mes\n" + "After:  \e[32mh\e[0ma\e[32mt\e[0mes\n" + "```");
    }

    [Test]
    public void BuildDeleteLog_IncludesPrecedingMessageLinkWhenProvided()
    {
        ModerationLogComponents.ModerationLogMessage log = ModerationLogComponents.BuildDeleteLog(
            2,
            4,
            new DateTimeOffset(2026, 7, 6, 10, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 7, 6, 11, 0, 0, TimeSpan.Zero),
            "https://discord.com/channels/1/2/3",
            "cached",
            Array.Empty<CachedAttachmentEvidence>()
        );

        string metadata = GetTextContent(log).Single(value => value.Contains("**Preceding Message**", StringComparison.Ordinal));

        metadata.ShouldContain("**Preceding Message**: [Jump to message](https://discord.com/channels/1/2/3)");
    }

    [Test]
    public void BuildDeleteLog_StillBuildsWhenPrecedingLookupReturnsNoResult()
    {
        ModerationLogComponents.ModerationLogMessage log = BuildDeleteLogWithoutPrecedingMessage();
        string[] text = GetTextContent(log);

        text.ShouldContain("## Message Delete");
        text.ShouldNotContain(value => value.Contains("**Preceding Message**", StringComparison.Ordinal));
    }

    [Test]
    public void BuildDeleteLog_StillBuildsWhenPrecedingLookupFails()
    {
        ModerationLogComponents.ModerationLogMessage log = BuildDeleteLogWithoutPrecedingMessage();
        string[] text = GetTextContent(log);

        text.ShouldContain("## Message Delete");
        text.ShouldContain("```cached```");
    }

    [Test]
    public void BuildEditLog_OmitsAttachmentComponentsWhenEvidenceIsAbsent()
    {
        ModerationLogComponents.ModerationLogMessage log = ModerationLogComponents.BuildEditLog(
            "https://discord.com/channels/1/2/3",
            4,
            DateTimeOffset.UtcNow,
            "before",
            "after",
            Array.Empty<CachedAttachmentEvidence>()
        );

        ContainerComponent container = GetContainer(log);

        log.Attachments.ShouldBeEmpty();
        GetTextContent(log).ShouldNotContain("### Attachments");
        container.Components.OfType<MediaGalleryComponent>().ShouldBeEmpty();
        container.Components.OfType<FileComponent>().ShouldBeEmpty();
    }

    [Test]
    public void BuildEditLog_IncludesGalleryAndFileComponentsWhenEvidenceIsPresent()
    {
        CachedAttachmentEvidence image = new CachedAttachmentEvidence(1, new byte[] { 1, 2, 3 }, "image/png");
        CachedAttachmentEvidence file = new CachedAttachmentEvidence(2, new byte[] { 4, 5, 6 }, "application/pdf");

        ModerationLogComponents.ModerationLogMessage log = ModerationLogComponents.BuildEditLog(
            "https://discord.com/channels/1/2/3",
            4,
            DateTimeOffset.UtcNow,
            "before",
            "after",
            new[] { image, file }
        );

        ContainerComponent container = GetContainer(log);
        MediaGalleryComponent gallery = container.Components.OfType<MediaGalleryComponent>().Single();
        FileComponent fileComponent = container.Components.OfType<FileComponent>().Single();

        GetTextContent(log).ShouldContain("### Attachments");
        log.Attachments.Select(attachment => attachment.FileName).ShouldBe(new[] { "evidence-1.png", "evidence-2.bin" });
        gallery.Items.Single().Media.Url.ShouldBe("attachment://evidence-1.png");
        fileComponent.File.Url.ShouldBe("attachment://evidence-2.bin");
    }

    [Test]
    public void BuildDeleteLog_ReferencesEvidenceAttachmentsInComponentModels()
    {
        CachedAttachmentEvidence video = new CachedAttachmentEvidence(3, new byte[] { 1 }, "video/mp4");
        CachedAttachmentEvidence file = new CachedAttachmentEvidence(4, new byte[] { 2 }, "text/plain");

        ModerationLogComponents.ModerationLogMessage log = ModerationLogComponents.BuildDeleteLog(
            2,
            4,
            null,
            DateTimeOffset.UtcNow,
            null,
            null,
            new[] { video, file }
        );

        ContainerComponent container = GetContainer(log);

        log.Attachments.Select(attachment => attachment.AttachmentUrl)
            .ShouldBe(new[] { "attachment://evidence-3.mp4", "attachment://evidence-4.bin" });
        container.Components.OfType<MediaGalleryComponent>().Single().Items.Single().Media.Url.ShouldBe("attachment://evidence-3.mp4");
        container.Components.OfType<FileComponent>().Single().File.Url.ShouldBe("attachment://evidence-4.bin");
    }

    private static ModerationLogComponents.ModerationLogMessage BuildDeleteLogWithoutPrecedingMessage() =>
        ModerationLogComponents.BuildDeleteLog(
            2,
            4,
            null,
            new DateTimeOffset(2026, 7, 6, 11, 0, 0, TimeSpan.Zero),
            null,
            "cached",
            Array.Empty<CachedAttachmentEvidence>()
        );

    private static ContainerComponent GetContainer(ModerationLogComponents.ModerationLogMessage log) =>
        log.Components.Components.Single().ShouldBeOfType<ContainerComponent>();

    private static string[] GetTextContent(ModerationLogComponents.ModerationLogMessage log) =>
        GetContainer(log).Components.OfType<TextDisplayComponent>().Select(component => component.Content).ToArray();
}
