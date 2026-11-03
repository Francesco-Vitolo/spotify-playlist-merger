namespace SpotifyLikeAll.Auth;

/// <summary>
/// Bound from the "Spotify" configuration section. Keep ClientId/ClientSecret in user secrets:
/// dotnet user-secrets set "Spotify:ClientId" "..." / "Spotify:ClientSecret" "..."
/// </summary>
public sealed class SpotifyOptions
{
    public const string Section = "Spotify";

    public string ClientId { get; set; } = "";
    public string ClientSecret { get; set; } = "";

    /// <summary>Must match the redirect URI registered in the Spotify dashboard exactly.</summary>
    public string RedirectUri { get; set; } = "http://127.0.0.1:8888/callback";

    public string Scopes { get; set; } =
        "playlist-read-private playlist-read-collaborative user-library-read user-library-modify";

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(ClientId) && !string.IsNullOrWhiteSpace(ClientSecret);
}
