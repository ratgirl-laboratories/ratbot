# Moderation Logging

RatBot moderation logging records attribution metadata for message create, edit, delete, and bulk-delete handling. The database stores guild logging configuration, message ids, guild ids, channel ids, author ids, observation timestamps, emitted log message ids, and excluded channels only.

The database does not store message content, attachment filenames, attachment URLs, attachment bytes, usernames, nicks, discriminators, or local file paths.

Message content and attachment bytes may be held briefly in an in-memory evidence cache so moderators can see useful evidence when an edit or delete happens. Retention is configured per guild with `/logging config`; process-wide message count, attachment count per message, bytes per attachment, and total attachment bytes remain environment-configured safety limits. Cache contents are lost on restart and can also be unavailable after expiry, attachment download failure, or when Discord does not provide message content.

Persisted attribution metadata is retained separately from the evidence cache. `Logging:MetadataRetentionPeriod` controls how long `ObservedMessage` and `MessageLogEntry` rows remain, and `Logging:MetadataCleanupInterval` controls the recurring cleanup cadence. Cleanup deletes rows older than the retention cutoff and is safe to run repeatedly.

Message Content privileged intent is required for content evidence. Without it, metadata logging can still work, but content evidence will usually be unavailable.

`/logging config` partially updates the guild's persisted logging policy. Omitted options keep their existing values. Enabling logging requires a delete or edit log channel. `/logging exclude` disables all observation, evidence caching, edit logs, delete logs, bulk-delete logs, and log-entry persistence for the selected channel. `/logging include` removes that exclusion. `/logging exclusions` lists the guild's persisted excluded channels.
