using System.Net.Http.Json;
using System.Text.Json;

namespace RatBot.Discord.Commands.AdventureLeaderboard;

public sealed class AdventureLeaderboardClient(IOptions<AdventureLeaderboardOptions> options)
{
    private static readonly HttpClient HttpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
    private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);

    private readonly AdventureLeaderboardOptions _options = options.Value;

    public async Task<IReadOnlyList<AdventureEntryDto>> GetLeaderboardAsync(int year, CancellationToken cancellationToken)
    {
        Uri uri = new Uri(new Uri(_options.BaseUrl, UriKind.Absolute), year.ToString());

        using HttpResponseMessage response = await HttpClient.GetAsync(uri, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        IReadOnlyList<AdventureEntryDto>? rows = await response
            .Content.ReadFromJsonAsync<IReadOnlyList<AdventureEntryDto>>(JsonOptions, cancellationToken)
            .ConfigureAwait(false);

        return rows ?? [];
    }
}
