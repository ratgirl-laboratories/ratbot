using System.Text.Json;
using LanguageExt;
using RatBot.Infrastructure.Data;
using Serilog;

namespace RatBot.Infrastructure.Services;

/// <summary>
/// Provides typed access to persisted bot configuration families stored as JSON.
/// </summary>
public sealed class JsonConfigRepository : IConfigRepository
{
    private static readonly JsonSerializerOptions JsonSerializerOptions = new JsonSerializerOptions();

    private readonly Dictionary<ConfigCacheKey, object> _cachedFamilies = new Dictionary<ConfigCacheKey, object>();
    private readonly IDbContextFactory<BotDbContext> _dbContextFactory;
    private readonly SemaphoreSlim _gate = new SemaphoreSlim(1, 1);
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="JsonConfigRepository"/> class.
    /// </summary>
    /// <param name="dbContextFactory">The db-context factory.</param>
    /// <param name="logger">The logger.</param>
    public JsonConfigRepository(IDbContextFactory<BotDbContext> dbContextFactory, ILogger logger)
    {
        _dbContextFactory = dbContextFactory;
        _logger = logger.ForContext<JsonConfigRepository>();
    }

    private static TConfig Deserialize<TConfig>(string family, string json)
        where TConfig : class =>
        JsonSerializer.Deserialize<TConfig>(json, JsonSerializerOptions)
        ?? throw new InvalidOperationException($"Stored config family '{family}' could not be deserialized as {typeof(TConfig).FullName}.");

    /// <inheritdoc />
    public async Task<Arr<TConfig>> GetFamilyAsync<TConfig>(string family, CancellationToken ct = default)
        where TConfig : class
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(family);

        ConfigCacheKey cacheKey = new ConfigCacheKey(family, typeof(TConfig));
        await _gate.WaitAsync(ct);

        try
        {
            if (TryGetCachedFamily(cacheKey, out Arr<TConfig> cachedValues))
                return cachedValues;

            await using BotDbContext dbContext = await _dbContextFactory.CreateDbContextAsync(ct);
            string[] payloads = await dbContext
                .ConfigEntries.AsNoTracking()
                .Where(entry => entry.Key == family)
                .OrderBy(entry => entry.Value)
                .Select(entry => entry.Value)
                .ToArrayAsync(ct);

            Arr<TConfig> configs = new Arr<TConfig>(payloads.Select(payload => Deserialize<TConfig>(family, payload)));
            SetCachedFamily(cacheKey, configs);

            _logger.Debug("Loaded {ConfigCount} config entries for {ConfigFamily}.", configs.Length, family);
            return configs;
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <inheritdoc />
    public async Task ReplaceFamilyAsync<TConfig>(string family, Arr<TConfig> values, CancellationToken ct = default)
        where TConfig : class
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(family);

        await _gate.WaitAsync(ct);

        try
        {
            Arr<TConfig> configs = values;
            string[] payloads = configs.Select(value => JsonSerializer.Serialize(value, JsonSerializerOptions)).Distinct(StringComparer.Ordinal).ToArray();

            await using BotDbContext dbContext = await _dbContextFactory.CreateDbContextAsync(ct);
            List<ConfigEntry> existingEntries = await dbContext.ConfigEntries.Where(entry => entry.Key == family).ToListAsync(ct);

            if (existingEntries.Count != 0)
                dbContext.ConfigEntries.RemoveRange(existingEntries);

            if (payloads.Length != 0)
                dbContext.ConfigEntries.AddRange(payloads.Select(payload => new ConfigEntry { Key = family, Value = payload }));

            await dbContext.SaveChangesAsync(ct);
            SetCachedFamily(new ConfigCacheKey(family, typeof(TConfig)), configs);

            _logger.Debug("Replaced {ConfigCount} config entries for {ConfigFamily}.", configs.Length, family);
        }
        finally
        {
            _gate.Release();
        }
    }

    private bool TryGetCachedFamily<TConfig>(ConfigCacheKey cacheKey, out Arr<TConfig> values)
        where TConfig : class
    {
        if (_cachedFamilies.TryGetValue(cacheKey, out object? cachedValues))
        {
            values = (Arr<TConfig>)cachedValues;
            return true;
        }

        values = Arr<TConfig>.Empty;
        return false;
    }

    private void SetCachedFamily<TConfig>(ConfigCacheKey cacheKey, Arr<TConfig> values)
        where TConfig : class
    {
        _cachedFamilies[cacheKey] = values;
    }

    private readonly record struct ConfigCacheKey(string Family, Type ConfigType);
}
