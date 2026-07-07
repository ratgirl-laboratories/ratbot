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
            2,
            4,
            beforeContent: null,
            afterContent: null,
            Array.Empty<CachedAttachmentEvidence>()
        );

        log.Embed.Title.ShouldBeNull();
        log.Embed.Timestamp.ShouldBeNull();
        log.Embed.Description.ShouldBe("```ansi\n*Unavailable*\n```\n```ansi\n*Unavailable*\n```");
        log.Embed.Description.ShouldNotContain("Content");
        log.Embed.Description.ShouldNotContain("Before:");
        log.Embed.Description.ShouldNotContain("After:");
    }

    [Test]
    public void BuildEditLog_RendersMessageDiffAsAnsiBlock()
    {
        ModerationLogComponents.ModerationLogMessage log = ModerationLogComponents.BuildEditLog(
            "https://discord.com/channels/1/2/3",
            2,
            4,
            "Hayes",
            "hates",
            Array.Empty<CachedAttachmentEvidence>()
        );

        log.Embed.Description.ShouldBe("```ansi\n\e[1;31mH\e[0ma\e[1;31my\e[0mes\n```\n```ansi\n\e[1;32mh\e[0ma\e[1;32mt\e[0mes\n```");
        log.Embed.Description.ShouldNotContain("Before:");
        log.Embed.Description.ShouldNotContain("After:");
    }

    [Test]
    public void BuildEditLog_RendersContextFieldsInline()
    {
        ModerationLogComponents.ModerationLogMessage log = ModerationLogComponents.BuildEditLog(
            "https://discord.com/channels/1/2/3",
            2,
            4,
            "Hayes",
            "hates",
            Array.Empty<CachedAttachmentEvidence>()
        );

        EmbedField[] fields = log.Embed.Fields.ToArray();

        log.Embed.Color.ShouldBe(new Color(14399750));
        fields.Select(field => field.Name).ShouldBe(new[] { "Author", "Message", "Channel" });
        fields.Select(field => field.Value).ShouldBe(new[] { "<@4> (`4`)", "[Link](https://discord.com/channels/1/2/3)", "<#2>" });
        fields.ShouldAllBe(field => field.Inline);
    }

    [Test]
    public void BuildDeleteLog_UsesPrecedingMessageLinkWhenProvided()
    {
        ModerationLogComponents.ModerationLogMessage log = ModerationLogComponents.BuildDeleteLog(
            2,
            4,
            "https://discord.com/channels/1/2/3",
            "cached",
            Array.Empty<CachedAttachmentEvidence>()
        );

        EmbedField[] fields = log.Embed.Fields.ToArray();

        log.Embed.Color.ShouldBe(new Color(0xFF0000));
        log.Embed.Timestamp.ShouldBeNull();
        fields.Select(field => field.Name).ShouldBe(new[] { "Author", "Message", "Channel" });
        fields.Select(field => field.Value).ShouldBe(new[] { "<@4> (`4`)", "[Link](https://discord.com/channels/1/2/3)", "<#2>" });
        fields.ShouldAllBe(field => field.Inline);
    }

    [Test]
    public void BuildDeleteLog_UsesUnavailableMessageFieldWhenNoPrecedingMessageExists()
    {
        ModerationLogComponents.ModerationLogMessage log = BuildDeleteLogWithoutPrecedingMessage();

        log.Embed.Fields.Single(field => string.Equals(field.Name, "Message", StringComparison.Ordinal)).Value.ShouldBe("Unavailable");
    }

    [Test]
    public void BuildDeleteLog_RendersCachedContentInPlainCodeBlock()
    {
        ModerationLogComponents.ModerationLogMessage log = BuildDeleteLogWithoutPrecedingMessage();

        log.Embed.Title.ShouldBeNull();
        log.Embed.Timestamp.ShouldBeNull();
        log.Embed.Description.ShouldBe("```cached```");
        log.Embed.Description.ShouldNotContain("Observed");
        log.Embed.Description.ShouldNotContain("Deleted");
    }

    [Test]
    public void BuildDeleteLog_RendersUnavailableWhenContentIsMissing()
    {
        ModerationLogComponents.ModerationLogMessage log = ModerationLogComponents.BuildDeleteLog(
            2,
            4,
            precedingMessageJumpUrl: null,
            content: null,
            Array.Empty<CachedAttachmentEvidence>()
        );

        log.Embed.Description.ShouldBe("*Unavailable*");
    }

    [Test]
    public void BuildEditLog_KeepsEvidenceAttachmentsForUploadOnly()
    {
        CachedAttachmentEvidence image = new CachedAttachmentEvidence(1, new byte[] { 1, 2, 3 }, "image/png");
        CachedAttachmentEvidence file = new CachedAttachmentEvidence(2, new byte[] { 4, 5, 6 }, "application/pdf");

        ModerationLogComponents.ModerationLogMessage log = ModerationLogComponents.BuildEditLog(
            "https://discord.com/channels/1/2/3",
            2,
            4,
            "before",
            "after",
            new[] { image, file }
        );

        log.Attachments.Select(attachment => attachment.FileName).ShouldBe(new[] { "evidence-1.png", "evidence-2.bin" });
        log.Attachments.Select(attachment => attachment.AttachmentUrl)
            .ShouldBe(new[] { "attachment://evidence-1.png", "attachment://evidence-2.bin" });
    }

    [Test]
    public void BuildDeleteLog_KeepsEvidenceAttachmentsForUploadOnly()
    {
        CachedAttachmentEvidence video = new CachedAttachmentEvidence(3, new byte[] { 1 }, "video/mp4");
        CachedAttachmentEvidence file = new CachedAttachmentEvidence(4, new byte[] { 2 }, "text/plain");

        ModerationLogComponents.ModerationLogMessage log = ModerationLogComponents.BuildDeleteLog(
            2,
            4,
            precedingMessageJumpUrl: null,
            content: null,
            new[] { video, file }
        );

        log.Attachments.Select(attachment => attachment.FileName).ShouldBe(new[] { "evidence-3.mp4", "evidence-4.bin" });
        log.Attachments.Select(attachment => attachment.AttachmentUrl)
            .ShouldBe(new[] { "attachment://evidence-3.mp4", "attachment://evidence-4.bin" });
    }

    private static ModerationLogComponents.ModerationLogMessage BuildDeleteLogWithoutPrecedingMessage() =>
        ModerationLogComponents.BuildDeleteLog(2, 4, precedingMessageJumpUrl: null, "cached", Array.Empty<CachedAttachmentEvidence>());
}
