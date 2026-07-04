using RatBot.Domain.Features.Logging;

namespace RatBot.Infrastructure.Features.Logging.Persistence;

public sealed class MessageLogEntryConfiguration : IEntityTypeConfiguration<MessageLogEntry>
{
    public void Configure(EntityTypeBuilder<MessageLogEntry> builder)
    {
        builder.ToTable("message_log_entries");
        builder.HasKey(entry => new { entry.OriginalMessageId, entry.LogMessageId });
        builder.Property(entry => entry.OriginalMessageId).HasColumnName("original_message_id");
        builder.Property(entry => entry.LogMessageId).HasColumnName("log_message_id");
        builder.Property(entry => entry.CapturedAtUtc).HasColumnName("captured_at_utc").IsRequired();
        builder.HasIndex(entry => entry.LogMessageId);
    }
}
