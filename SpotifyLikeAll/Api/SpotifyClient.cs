using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text.Json;
using SpotifyLikeAll.Auth;

namespace SpotifyLikeAll.Api;

public sealed class SpotifyApiException(string message, HttpStatusCode? statusCode = null) : Exception(message)
{
    public HttpStatusCode? StatusCode { get; } = statusCode;
}

public sealed class SpotifyClient(HttpClient http, SpotifyAuthService auth) : ISpotifyClient
{
    private const int PageSize = 50;
    private const int MaxRetries = 5;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true,
    };

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
        var forceRefresh = false;
        var refreshedAfter401 = false;

        for (var attempt = 1; ; attempt++)
        {
            // Throws NotLoggedInException when there is no (valid) Spotify session.
            var accessToken = await auth.GetAccessTokenAsync(forceRefresh, ct);
            forceRefresh = false;

            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            using var response = await http.SendAsync(request, ct);

            if (response.StatusCode == HttpStatusCode.Unauthorized && !refreshedAfter401)
            {
                // Token was rejected before its expected expiry: refresh once and retry.
                refreshedAfter401 = true;
                forceRefresh = true;
                continue;
            }

            if (response.StatusCode == HttpStatusCode.TooManyRequests && attempt <= MaxRetries)
            {
                var delay = response.Headers.RetryAfter?.Delta ?? TimeSpan.FromSeconds(5);
                await Task.Delay(delay, ct);
                continue;
            }

            if (response.StatusCode == HttpStatusCode.Unauthorized)
                throw new SpotifyApiException(
                    "Spotify rejected the access token even after refreshing it. Try logging out and in again.",
                    HttpStatusCode.Unauthorized);

            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(ct);
                throw new SpotifyApiException(
                    $"{(int)response.StatusCode} {response.StatusCode} for {url}: {body}", response.StatusCode);
            }

            return await response.Content.ReadFromJsonAsync<T>(JsonOptions, ct)
                   ?? throw new SpotifyApiException($"Empty response for {url}");
        }
    }
}
