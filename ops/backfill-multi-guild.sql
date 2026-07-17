\set ON_ERROR_STOP on

-- Usage:
--   psql "$DB__CONNECTION_STRING" -v legacy_guild_id=123456789012345678 -f ops/backfill-multi-guild.sql
-- The legacy_guild_id is intentionally supplied by the operator; ratbot source and migrations do not guess it.

BEGIN;

CREATE TEMP TABLE multi_guild_backfill_input AS
SELECT :'legacy_guild_id'::bigint AS legacy_guild_id;

DO $$
DECLARE
    supplied_guild_id bigint;
BEGIN
    SELECT legacy_guild_id INTO supplied_guild_id FROM multi_guild_backfill_input;

    IF supplied_guild_id IS NULL OR supplied_guild_id <= 0 THEN
        RAISE EXCEPTION 'legacy_guild_id must be a positive Discord guild snowflake';
    END IF;
END $$;

UPDATE "RoleColourOptions"
SET "GuildId" = (SELECT legacy_guild_id FROM multi_guild_backfill_input)
WHERE "GuildId" IS NULL;

UPDATE "MemberColourPreferences"
SET "GuildId" = (SELECT legacy_guild_id FROM multi_guild_backfill_input)
WHERE "GuildId" IS NULL;

UPDATE "ImageSpamSettings"
SET
    "GuildId" = COALESCE("GuildId", (SELECT legacy_guild_id FROM multi_guild_backfill_input)),
    "IsEnabled" = COALESCE("IsEnabled", TRUE)
WHERE "GuildId" IS NULL OR "IsEnabled" IS NULL;

UPDATE "EmojiUsageCounts"
SET "GuildId" = (SELECT legacy_guild_id FROM multi_guild_backfill_input)
WHERE "GuildId" IS NULL;

UPDATE "AdventureForumThreadLinks"
SET "GuildId" = (SELECT legacy_guild_id FROM multi_guild_backfill_input)
WHERE "GuildId" IS NULL;

UPDATE message_log_entries
SET guild_id = (SELECT legacy_guild_id FROM multi_guild_backfill_input)
WHERE guild_id IS NULL;

-- Verification result set: every count must be zero before contract migration.
SELECT 'role_colour_options_missing_guild' AS check_name, COUNT(*) AS failing_rows FROM "RoleColourOptions" WHERE "GuildId" IS NULL
UNION ALL
SELECT 'member_colour_preferences_missing_guild', COUNT(*) FROM "MemberColourPreferences" WHERE "GuildId" IS NULL
UNION ALL
SELECT 'image_spam_settings_missing_guild_or_enabled', COUNT(*) FROM "ImageSpamSettings" WHERE "GuildId" IS NULL OR "IsEnabled" IS NULL
UNION ALL
SELECT 'emoji_usage_counts_missing_guild', COUNT(*) FROM "EmojiUsageCounts" WHERE "GuildId" IS NULL
UNION ALL
SELECT 'adventure_thread_links_missing_guild', COUNT(*) FROM "AdventureForumThreadLinks" WHERE "GuildId" IS NULL
UNION ALL
SELECT 'message_log_entries_missing_guild', COUNT(*) FROM message_log_entries WHERE guild_id IS NULL
UNION ALL
SELECT 'role_colour_duplicate_final_keys', COUNT(*) FROM (
    SELECT 1 FROM "RoleColourOptions" GROUP BY "GuildId", "NormalisedKey" HAVING COUNT(*) > 1
) duplicates
UNION ALL
SELECT 'member_colour_duplicate_final_keys', COUNT(*) FROM (
    SELECT 1 FROM "MemberColourPreferences" GROUP BY "GuildId", "UserId" HAVING COUNT(*) > 1
) duplicates
UNION ALL
SELECT 'emoji_usage_duplicate_final_keys', COUNT(*) FROM (
    SELECT 1 FROM "EmojiUsageCounts" GROUP BY "GuildId", "EmojiId" HAVING COUNT(*) > 1
) duplicates
UNION ALL
SELECT 'adventure_thread_duplicate_final_keys', COUNT(*) FROM (
    SELECT 1 FROM "AdventureForumThreadLinks" GROUP BY "GuildId", "ScorePartIndex" HAVING COUNT(*) > 1
) duplicates
UNION ALL
SELECT 'adventure_leaderboard_duplicate_final_keys', COUNT(*) FROM (
    SELECT 1 FROM "AdventureLeaderboardMessageState" GROUP BY "GuildId", "Id" HAVING COUNT(*) > 1
) duplicates
UNION ALL
SELECT 'member_colour_cross_guild_selected_option', COUNT(*) FROM "MemberColourPreferences" p
JOIN "RoleColourOptions" o ON o."OptionId" = p."SelectedOptionId"
WHERE p."SelectedOptionId" IS NOT NULL AND p."GuildId" <> o."GuildId"
UNION ALL
SELECT 'message_log_entries_missing_observed_message', COUNT(*) FROM message_log_entries entry
LEFT JOIN observed_messages observed
    ON observed.guild_id = entry.guild_id
    AND observed.original_message_id = entry.original_message_id
WHERE observed.original_message_id IS NULL;

DO $$
BEGIN
    IF EXISTS (SELECT 1 FROM "RoleColourOptions" WHERE "GuildId" IS NULL)
        OR EXISTS (SELECT 1 FROM "MemberColourPreferences" WHERE "GuildId" IS NULL)
        OR EXISTS (SELECT 1 FROM "ImageSpamSettings" WHERE "GuildId" IS NULL OR "IsEnabled" IS NULL)
        OR EXISTS (SELECT 1 FROM "EmojiUsageCounts" WHERE "GuildId" IS NULL)
        OR EXISTS (SELECT 1 FROM "AdventureForumThreadLinks" WHERE "GuildId" IS NULL)
        OR EXISTS (SELECT 1 FROM message_log_entries WHERE guild_id IS NULL)
        OR EXISTS (SELECT 1 FROM "RoleColourOptions" GROUP BY "GuildId", "NormalisedKey" HAVING COUNT(*) > 1)
        OR EXISTS (SELECT 1 FROM "MemberColourPreferences" GROUP BY "GuildId", "UserId" HAVING COUNT(*) > 1)
        OR EXISTS (SELECT 1 FROM "EmojiUsageCounts" GROUP BY "GuildId", "EmojiId" HAVING COUNT(*) > 1)
        OR EXISTS (SELECT 1 FROM "AdventureForumThreadLinks" GROUP BY "GuildId", "ScorePartIndex" HAVING COUNT(*) > 1)
        OR EXISTS (SELECT 1 FROM "AdventureLeaderboardMessageState" GROUP BY "GuildId", "Id" HAVING COUNT(*) > 1)
        OR EXISTS (
            SELECT 1 FROM "MemberColourPreferences" p
            JOIN "RoleColourOptions" o ON o."OptionId" = p."SelectedOptionId"
            WHERE p."SelectedOptionId" IS NOT NULL AND p."GuildId" <> o."GuildId"
        )
        OR EXISTS (
            SELECT 1 FROM message_log_entries entry
            LEFT JOIN observed_messages observed
                ON observed.guild_id = entry.guild_id
                AND observed.original_message_id = entry.original_message_id
            WHERE observed.original_message_id IS NULL
        ) THEN
        RAISE EXCEPTION 'multi-guild backfill verification failed; see verification rows above';
    END IF;
END $$;

COMMIT;
