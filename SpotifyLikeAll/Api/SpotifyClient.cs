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

    public IAsyncEnumerable<SavedTrackDto> GetSavedTracksAsync(CancellationToken ct = default) =>
        GetAllPagesAsync<SavedTrackDto>($"me/tracks?limit={PageSize}", ct);

    public async Task SaveAsync(IReadOnlyList<string> uris, CancellationToken ct = default)
    {
        if (uris.Count == 0)
            return;

        using var response = await SendAsync(HttpMethod.Put, $"me/library?uris={JoinUris(uris)}", ct);
    }

    public async Task RemoveAsync(IReadOnlyList<string> uris, CancellationToken ct = default)
    {
        if (uris.Count == 0)
            return;

        using var response = await SendAsync(HttpMethod.Delete, $"me/library?uris={JoinUris(uris)}", ct);
    }

    private static string JoinUris(IReadOnlyList<string> uris)
    {
        if (uris.Count > ISpotifyClient.LibraryBatchSize)
            throw new ArgumentException($"At most {ISpotifyClient.LibraryBatchSize} URIs per request.", nameof(uris));

        return string.Join(",", uris.Select(uri => Uri.EscapeDataString(uri)));
    }

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
        using var response = await SendAsync(HttpMethod.Get, url, ct);
        return await response.Content.ReadFromJsonAsync<T>(JsonOptions, ct)
               ?? throw new SpotifyApiException($"Empty response for {url}");
    }

    /// <summary>
    /// Sends a request with a valid access token. Retries on 429 (Retry-After) and refreshes the
    /// token once on an unexpected 401. Returns only successful responses; the caller disposes it.
    /// </summary>
    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string url, CancellationToken ct)
    {
        var forceRefresh = false;
        var refreshedAfter401 = false;

        for (var attempt = 1; ; attempt++)
        {
            // Throws NotLoggedInException when there is no (valid) Spotify session.
            var accessToken = await auth.GetAccessTokenAsync(forceRefresh, ct);
            forceRefresh = false;

            using var request = new HttpRequestMessage(method, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            if (method != HttpMethod.Get)
                request.Content = new ByteArrayContent([]); // explicit empty body (Content-Length: 0)

            var response = await http.SendAsync(request, ct);

            if (response.IsSuccessStatusCode)
                return response;

            using (response)
            {
                if (response.StatusCode == HttpStatusCode.TooManyRequests && attempt <= MaxRetries)
                {
                    var delay = response.Headers.RetryAfter?.Delta ?? TimeSpan.FromSeconds(5);
                    await Task.Delay(delay, ct);
                    continue;
                }

                if (response.StatusCode == HttpStatusCode.Unauthorized && !refreshedAfter401)
                {
                    // Token was rejected before its expected expiry: refresh once and retry.
                    refreshedAfter401 = true;
                    forceRefresh = true;
                    continue;
                }

                if (response.StatusCode == HttpStatusCode.Unauthorized)
                    throw new SpotifyApiException(
                        "Spotify rejected the access token even after refreshing it. Try logging out and in again.",
                        HttpStatusCode.Unauthorized);

                var body = await response.Content.ReadAsStringAsync(ct);

                if (response.StatusCode == HttpStatusCode.Forbidden && url.StartsWith("me/library"))
                    throw new SpotifyApiException(
                        $"403 Forbidden for {method} {url}. If you logged in before the like feature existed, " +
                        $"log out and in again so the app gets the library permissions. Details: {body}",
                        response.StatusCode);

                throw new SpotifyApiException(
                    $"{(int)response.StatusCode} {response.StatusCode} for {method} {url}: {body}", response.StatusCode);
            }
        }
    }
}
