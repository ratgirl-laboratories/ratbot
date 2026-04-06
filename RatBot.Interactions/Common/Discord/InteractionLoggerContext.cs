namespace RatBot.Interactions.Common.Discord;

/// <summary>
/// Builds consistent structured logger contexts for interaction handling code.
/// </summary>
public static class InteractionLoggerContext
{
    /// <summary>
    /// Creates a logger enriched with source, method, and interaction identity fields.
    /// </summary>
    /// <param name="context">The active interaction context.</param>
    /// <param name="sourceContext">The logical source context value.</param>
    /// <param name="methodContext">The method context value.</param>
    /// <returns>The enriched logger.</returns>
    public static ILogger Create(
        SocketInteractionContext context,
        string? sourceContext,
        string methodContext)
        => Log.ForContext("SourceContext", sourceContext)
            .ForContext("method_context", methodContext)
            .ForContext("interaction_id", context.Interaction.Id)
            .ForContext("interaction_type", context.Interaction.Type.ToString())
            .ForContext("interaction_created_at_utc", context.Interaction.CreatedAt.UtcDateTime.ToString("O"))
            .ForContext("user_id", context.User.Id)
            .ForContext("guild_id", context.Guild?.Id)
            .ForContext("channel_id", context.Channel?.Id);
}
