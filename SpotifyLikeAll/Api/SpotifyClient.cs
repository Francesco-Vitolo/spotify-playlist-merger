using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text.Json;

namespace SpotifyLikeAll.Api;

public sealed class SpotifyApiException(string message) : Exception(message);

public sealed class SpotifyClient : ISpotifyClient
{
    private const int PageSize = 50;
    private const int MaxRetries = 5;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true,
    };

    private readonly HttpClient _http;

    public SpotifyClient(HttpClient http) => _http = http;

    public static SpotifyClient Create(string accessToken)
    {
        var http = new HttpClient { BaseAddress = new Uri("https://api.spotify.com/v1/") };
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return new SpotifyClient(http);
    }

    public Task<UserDto> GetCurrentUserAsync(CancellationToken ct = default) =>
        GetAsync<UserDto>("me", ct);

    public IAsyncEnumerable<PlaylistDto> GetMyPlaylistsAsync(CancellationToken ct = default) =>
        GetAllPagesAsync<PlaylistDto>($"me/playlists?limit={PageSize}", ct);

    public IAsyncEnumerable<PlaylistItemDto> GetPlaylistItemsAsync(string playlistId, CancellationToken ct = default) =>
        GetAllPagesAsync<PlaylistItemDto>(
            $"playlists/{Uri.EscapeDataString(playlistId)}/items?limit={PageSize}&additional_types=track", ct);

    private async IAsyncEnumerable<T> GetAllPagesAsync<T>(string url, [EnumeratorCancellation] CancellationToken ct)
    {
        string? next = url;
        while (next is not null)
        {
            var page = await GetAsync<PagingDto<T>>(next, ct);
            foreach (var item in page.Items ?? [])
                yield return item;

            // "next" is an absolute URL; HttpClient ignores BaseAddress for absolute URIs.
            next = page.Next;
        }
    }

    private async Task<T> GetAsync<T>(string url, CancellationToken ct)
    {
        for (var attempt = 1; ; attempt++)
        {
            using var response = await _http.GetAsync(url, ct);

            if (response.StatusCode == HttpStatusCode.TooManyRequests && attempt <= MaxRetries)
            {
                var delay = response.Headers.RetryAfter?.Delta ?? TimeSpan.FromSeconds(5);
                Console.Error.WriteLine($"Rate limited, waiting {delay.TotalSeconds:0}s ...");
                await Task.Delay(delay, ct);
                continue;
            }

            if (response.StatusCode == HttpStatusCode.Unauthorized)
                throw new SpotifyApiException(
                    "401 Unauthorized: the access token is missing, invalid or expired (tokens last 1 hour).");

            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(ct);
                throw new SpotifyApiException($"{(int)response.StatusCode} {response.StatusCode} for {url}: {body}");
            }

            return await response.Content.ReadFromJsonAsync<T>(JsonOptions, ct)
                   ?? throw new SpotifyApiException($"Empty response for {url}");
        }
    }
}
