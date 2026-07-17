using Microsoft.EntityFrameworkCore;
using RatBot.Application.Common.Interfaces;
using RatBot.Application.Reactions;
using RatBot.Domain.Adventure;
using RatBot.Domain.Emoji;
using RatBot.Domain.RoleColours;
using RatBot.Infrastructure.Data;
using RatBot.Infrastructure.RoleColours;
using RatBot.Infrastructure.Stores;
using Serilog;

namespace RatBot.Infrastructure.Tests.Integration;

[TestFixture]
public sealed class MultiGuildPersistenceTests
{
    [SetUp]
    public async Task SetUp() => await PostgresDatabaseFixture.ResetAsync();

    [Test]
    public async Task RoleColourState_IsIsolatedByGuild()
    {
        await using BotDbContext db = PostgresDatabaseFixture.CreateDbContext();
        RoleColourOperations operations = new RoleColourOperations(db);

        RoleColourOption guildA = (await operations.AddMappingAsync(1, "red", "Red", 10, 20, CancellationToken.None)).Value;
        RoleColourOption guildB = (await operations.AddMappingAsync(2, "red", "Red", 10, 20, CancellationToken.None)).Value;

        await operations.SelectOptionAsync(1, 100, guildA.OptionId, new ulong[] { 10 }, CancellationToken.None);
        await operations.SelectOptionAsync(2, 100, guildB.OptionId, new ulong[] { 10 }, CancellationToken.None);

        (await operations.ListConfiguredOptionsAsync(1, true, CancellationToken.None)).Value.Single().OptionId.ShouldBe(guildA.OptionId);
        (await operations.ListConfiguredOptionsAsync(2, true, CancellationToken.None)).Value.Single().OptionId.ShouldBe(guildB.OptionId);

        (await operations.DeleteMappingAsync(1, "red", CancellationToken.None)).IsError.ShouldBeFalse();

        (await operations.ListConfiguredOptionsAsync(1, true, CancellationToken.None)).Value.ShouldBeEmpty();
        (await operations.ListConfiguredOptionsAsync(2, true, CancellationToken.None)).Value.Single().OptionId.ShouldBe(guildB.OptionId);

        MemberColourPreference guildBPreference = await db.MemberColourPreferences.SingleAsync(x => x.GuildId == 2 && x.UserId == 100);
        guildBPreference.SelectedOptionId.ShouldBe(guildB.OptionId);
    }

    [Test]
    public async Task RoleColourPreference_CannotSelectOptionFromAnotherGuild()
    {
        await using BotDbContext db = PostgresDatabaseFixture.CreateDbContext();
        RoleColourOption guildB = RoleColourOption.Create(2, "blue", "Blue", 11, 21).Value;
        db.RoleColourOptions.Add(guildB);
        db.MemberColourPreferences.Add(MemberColourPreference.CreateForOption(1, 100, guildB.OptionId));

        DbUpdateException exception = await Should.ThrowAsync<DbUpdateException>(() => db.SaveChangesAsync());
        exception.Message.ShouldNotBeNullOrWhiteSpace();
    }

    [Test]
    public async Task ImageSpamSettings_ArePerGuild()
    {
        await using BotDbContext db = PostgresDatabaseFixture.CreateDbContext();
        ImageSpamSettingsStore imageStore = new ImageSpamSettingsStore(db);

        await imageStore.UpsertAsync(1, 4, 2, 45, CancellationToken.None);
        await imageStore.UpsertAsync(2, 2, 5, 60, CancellationToken.None);

        (await imageStore.GetAsync(1, CancellationToken.None))!.RequiredChannelCount.ShouldBe(4);
        (await imageStore.GetAsync(2, CancellationToken.None))!.RequiredAttachmentCount.ShouldBe(5);
    }

    [Test]
    public async Task AdventureAndEmojiState_ArePerGuild()
    {
        await using BotDbContext db = PostgresDatabaseFixture.CreateDbContext();
        db.AdventureForumThreadLinks.Add(AdventureForumThreadLink.Create(1, 1, 101));
        db.AdventureForumThreadLinks.Add(AdventureForumThreadLink.Create(2, 1, 201));
        db.AdventureLeaderboardMessageState.Add(AdventureLeaderboardMessageState.Create(1, 1, 10, 1000, 2026, "a"));
        db.AdventureLeaderboardMessageState.Add(AdventureLeaderboardMessageState.Create(1, 2, 20, 2000, 2026, "b"));
        AdventureSettings guildASettings = AdventureSettings.Create(1, 10001);
        AdventureSettings guildBSettings = AdventureSettings.Create(2, 20001);
        db.AdventureSettings.AddRange(guildASettings, guildBSettings);
        db.EmojiUsageCounts.Add(
            new EmojiUsageCount
            {
                GuildId = 1,
                EmojiId = 500,
                MessageUsageCount = 1,
                ReactionUsageCount = 1,
            }
        );
        db.EmojiUsageCounts.Add(
            new EmojiUsageCount
            {
                GuildId = 2,
                EmojiId = 500,
                MessageUsageCount = 3,
                ReactionUsageCount = 5,
            }
        );
        await db.SaveChangesAsync();

        (await db.AdventureForumThreadLinks.SingleAsync(x => x.GuildId == 1 && x.ScorePartIndex == 1)).ThreadId.ShouldBe(101UL);
        (await db.AdventureForumThreadLinks.SingleAsync(x => x.GuildId == 2 && x.ScorePartIndex == 1)).ThreadId.ShouldBe(201UL);
        (await db.AdventureLeaderboardMessageState.SingleAsync(x => x.GuildId == 1 && x.Id == 1)).MessageId.ShouldBe(1000UL);
        (await db.AdventureLeaderboardMessageState.SingleAsync(x => x.GuildId == 2 && x.Id == 1)).MessageId.ShouldBe(2000UL);
        (await db.AdventureSettings.SingleAsync(x => x.GuildId == 1)).AdventurerRoleId.ShouldBe(10001UL);
        (await db.AdventureSettings.SingleAsync(x => x.GuildId == 2)).AdventurerRoleId.ShouldBe(20001UL);

        db.ChangeTracker.Clear();
        AdventureSettings persistedGuildASettings = await db.AdventureSettings.SingleAsync(x => x.GuildId == 1);
        persistedGuildASettings.UpdateAdventurerRole(10002);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        (await db.AdventureSettings.SingleAsync(x => x.GuildId == 1)).AdventurerRoleId.ShouldBe(10002UL);
        (await db.AdventureSettings.SingleAsync(x => x.GuildId == 2)).AdventurerRoleId.ShouldBe(20001UL);

        ReactionUsageTracker tracker = new ReactionUsageTracker(db, new StaticTrackedEmojiCatalog(), Log.Logger);
        (await tracker.GetUsagePageAsync(1, 1, ct: CancellationToken.None)).Value.Items.Single().ReactionUsageCount.ShouldBe(1);
        (await tracker.GetUsagePageAsync(2, 1, ct: CancellationToken.None)).Value.Items.Single().ReactionUsageCount.ShouldBe(5);

        ReactionUsageTracker pruningTracker = new ReactionUsageTracker(db, new PruningTrackedEmojiCatalog(), Log.Logger);
        await pruningTracker.RecordBatchUsageAsync(1, new ulong[] { 501 }, CancellationToken.None);

        (await db.EmojiUsageCounts.AnyAsync(x => x.GuildId == 1 && x.EmojiId == 500)).ShouldBeFalse();
        (await db.EmojiUsageCounts.SingleAsync(x => x.GuildId == 2 && x.EmojiId == 500)).MessageUsageCount.ShouldBe(3);
    }

    private sealed class StaticTrackedEmojiCatalog : ITrackedEmojiCatalog
    {
        public bool TryGetTrackedEmojiIds(ulong guildId, out IReadOnlyCollection<ulong> emojiIds)
        {
            emojiIds = new ulong[] { 500 };
            return true;
        }
    }

    private sealed class PruningTrackedEmojiCatalog : ITrackedEmojiCatalog
    {
        public bool TryGetTrackedEmojiIds(ulong guildId, out IReadOnlyCollection<ulong> emojiIds)
        {
            emojiIds = guildId == 1 ? new ulong[] { 501 } : new ulong[] { 500 };
            return true;
        }
    }
}
