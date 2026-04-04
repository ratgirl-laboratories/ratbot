using Microsoft.Extensions.Configuration;

namespace RatBot.Infrastructure.Data;

/// <summary>
/// Builds PostgreSQL connection strings from environment-backed configuration.
/// </summary>
public static class PostgresConnectionStringBuilder
{
    /// <summary>
    /// Builds a PostgreSQL connection string from configured values.
    /// </summary>
    /// <param name="config">The application configuration root.</param>
    /// <returns>The PostgreSQL connection string.</returns>
    public static string Build(IConfiguration config)
    {
        string? connectionString = config["DB:ConnectionString"];
        if (!string.IsNullOrWhiteSpace(connectionString))
            return connectionString.StartsWith("jdbc:postgresql://", StringComparison.OrdinalIgnoreCase)
                ? ConvertJdbcToPostgresConnectionString(connectionString)
                : connectionString;

        string host = config["DB:Host"] ?? throw new InvalidOperationException("DB:Host not configured");
        string port = config["DB:Port"] ?? "5432";
        string database = config["DB:Database"] ?? throw new InvalidOperationException("DB:Database not configured");
        string user = config["DB:User"] ?? throw new InvalidOperationException("DB:User not configured");
        string password = config["DB:Password"] ?? throw new InvalidOperationException("DB:Password not configured");

        return $"Host={host};Port={port};Database={database};Username={user};Password={password};SSL Mode=Prefer";
    }

    private static string ConvertJdbcToPostgresConnectionString(string jdbcString)
    {
        try
        {
            Uri uri = new Uri(jdbcString.Replace("jdbc:", string.Empty, StringComparison.OrdinalIgnoreCase));
            string database = uri.AbsolutePath.TrimStart('/');
            string host = uri.Host;
            int port = uri.Port == -1 ? 5432 : uri.Port;

            Dictionary<string, string> queryValues = uri
                .Query.TrimStart('?')
                .Split('&', StringSplitOptions.RemoveEmptyEntries)
                .Select(part => part.Split('=', 2, StringSplitOptions.TrimEntries))
                .Where(parts => parts.Length == 2)
                .ToDictionary(parts => parts[0], parts => Uri.UnescapeDataString(parts[1]), StringComparer.OrdinalIgnoreCase);

            if (!queryValues.TryGetValue("user", out string? user) || string.IsNullOrWhiteSpace(user))
                throw new InvalidOperationException("The JDBC connection string is missing the 'user' query parameter.");

            if (!queryValues.TryGetValue("password", out string? password) || string.IsNullOrWhiteSpace(password))
                throw new InvalidOperationException("The JDBC connection string is missing the 'password' query parameter.");

            string sslMode =
                queryValues.TryGetValue("sslmode", out string? configuredSslMode) && !string.IsNullOrWhiteSpace(configuredSslMode)
                    ? configuredSslMode
                    : "Prefer";

            return $"Host={host};Port={port};Database={database};Username={user};Password={password};SSL Mode={sslMode}";
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to parse JDBC PostgreSQL connection string: {ex.Message}", ex);
        }
    }
}
