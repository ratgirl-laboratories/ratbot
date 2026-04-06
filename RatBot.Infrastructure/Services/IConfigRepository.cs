using LanguageExt;

namespace RatBot.Infrastructure.Services;

/// <summary>
/// Provides typed access to persisted bot configuration families.
/// </summary>
public interface IConfigRepository
{
    /// <summary>
    /// Gets the current configs for a config family.
    /// </summary>
    /// <typeparam name="TConfig">The config value type.</typeparam>
    /// <param name="family">The config family key.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The loaded config family.</returns>
    Task<Arr<TConfig>> GetFamilyAsync<TConfig>(string family, CancellationToken ct = default)
        where TConfig : class;

    /// <summary>
    /// Replaces all persisted configs for a config family.
    /// </summary>
    /// <typeparam name="TConfig">The config value type.</typeparam>
    /// <param name="family">The config family key.</param>
    /// <param name="values">The complete set of config values to persist.</param>
    /// <param name="ct">The cancellation token.</param>
    Task ReplaceFamilyAsync<TConfig>(string family, Arr<TConfig> values, CancellationToken ct = default)
        where TConfig : class;
}
