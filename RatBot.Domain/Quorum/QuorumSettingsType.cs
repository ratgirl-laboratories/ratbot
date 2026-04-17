namespace RatBot.Domain.Quorum;

/// <summary>
///     Identifies the level used for quorum configuration.
/// </summary>
public enum QuorumSettingsType
{
    /// <summary>
    ///     Configuration corresponds to an individual text channel.
    /// </summary>
    Channel = 1,

    /// <summary>
    ///     Configuration corresponds to a channel category.
    /// </summary>
    Category = 2
}