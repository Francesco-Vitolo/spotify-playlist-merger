using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace SpotifyLikeAll.Auth;

public sealed class NotLoggedInException(string message = "Not logged in to Spotify.") : Exception(message);

public sealed class SpotifyAuthException(HttpStatusCode statusCode, string body)
    : Exception($"Spotify login failed ({(int)statusCode}): {body}")
{
    public string Body { get; } = body;

    public bool IsInvalidGrant => Body.Contains("invalid_grant", StringComparison.OrdinalIgnoreCase);
}

/// <summary>
/// Authorization Code flow with client secret (safe here because the app runs server-side).
/// Holds the current access token in memory and refreshes it automatically shortly before it expires.
/// Single-user app → registered as a singleton.
/// </summary>
public sealed class SpotifyAuthService
{
    public const string HttpClientName = "spotify-accounts";
    private const string AccountsBase = "https://accounts.spotify.com/";
    private static readonly TimeSpan ExpiryBuffer = TimeSpan.FromMinutes(2);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    private readonly IHttpClientFactory _httpFactory;
    private readonly RefreshTokenStore _store;
    private readonly SpotifyOptions _options;
    private readonly SemaphoreSlim _lock = new(1, 1);

    private string? _refreshToken;
    private string? _accessToken;
    private DateTimeOffset _accessTokenExpiresAt;

    public SpotifyAuthService(IHttpClientFactory httpFactory, RefreshTokenStore store, IOptions<SpotifyOptions> options)
    {
        _httpFactory = httpFactory;
        _store = store;
        _options = options.Value;
        _refreshToken = store.Load();
    }

    public bool IsConfigured => _options.IsConfigured;

    public bool IsLoggedIn => _refreshToken is not null;

    public string BuildAuthorizeUrl(string state)
    {
        var query = QueryString.Create(new Dictionary<string, string?>
        {
            ["client_id"] = _options.ClientId,
            ["response_type"] = "code",
            ["redirect_uri"] = _options.RedirectUri,
            ["scope"] = _options.Scopes,
            ["state"] = state,
        });
        return AccountsBase + "authorize" + query;
    }

    /// <summary>Exchanges the code from the /callback redirect for access + refresh token.</summary>
    public async Task CompleteLoginAsync(string code, CancellationToken ct = default)
    {
        var token = await RequestTokenAsync(new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["code"] = code,
            ["redirect_uri"] = _options.RedirectUri,
        }, ct);

        await _lock.WaitAsync(ct);
        try
        {
            Apply(token);
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>Returns a valid access token, refreshing it when it is about to expire.</summary>
    public async Task<string> GetAccessTokenAsync(bool forceRefresh = false, CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct);
        try
        {
            if (!forceRefresh && _accessToken is not null &&
                DateTimeOffset.UtcNow < _accessTokenExpiresAt - ExpiryBuffer)
            {
                return _accessToken;
            }

            if (_refreshToken is null)
                throw new NotLoggedInException();

            TokenResponse token;
            try
            {
                token = await RequestTokenAsync(new Dictionary<string, string>
                {
                    ["grant_type"] = "refresh_token",
                    ["refresh_token"] = _refreshToken,
                }, ct);
            }
            catch (SpotifyAuthException ex) when (ex.IsInvalidGrant)
            {
                // Refresh token expired (180 days) or access was revoked.
                ClearSession();
                throw new NotLoggedInException("Your Spotify session has expired. Please log in again.");
            }

            Apply(token);
            return _accessToken!;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task LogoutAsync()
    {
        await _lock.WaitAsync();
        try
        {
            ClearSession();
        }
        finally
        {
            _lock.Release();
        }
    }

    private void Apply(TokenResponse token)
    {
        _accessToken = token.AccessToken;
        _accessTokenExpiresAt = DateTimeOffset.UtcNow.AddSeconds(token.ExpiresIn);

        // A refresh response may or may not contain a new refresh token; keep the old one if not.
        if (!string.IsNullOrEmpty(token.RefreshToken) && token.RefreshToken != _refreshToken)
        {
            _refreshToken = token.RefreshToken;
            _store.Save(token.RefreshToken);
        }
    }

    private void ClearSession()
    {
        _refreshToken = null;
        _accessToken = null;
        _store.Delete();
    }

    private async Task<TokenResponse> RequestTokenAsync(Dictionary<string, string> form, CancellationToken ct)
    {
        var http = _httpFactory.CreateClient(HttpClientName);

        using var request = new HttpRequestMessage(HttpMethod.Post, AccountsBase + "api/token")
        {
            Content = new FormUrlEncodedContent(form),
        };
        var basic = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_options.ClientId}:{_options.ClientSecret}"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", basic);

        using var response = await http.SendAsync(request, ct);
        var body = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
            throw new SpotifyAuthException(response.StatusCode, body);

        return JsonSerializer.Deserialize<TokenResponse>(body, JsonOptions)
               ?? throw new SpotifyAuthException(response.StatusCode, "Empty token response");
    }

    private sealed class TokenResponse
    {
        public string AccessToken { get; init; } = "";
        public int ExpiresIn { get; init; }
        public string? RefreshToken { get; init; }
    }
}
