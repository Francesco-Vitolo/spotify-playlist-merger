namespace SpotifyLikeAll.Services;

/// <summary>
/// Holds the Spotify user access token for this local, single-user app.
/// Initial value comes from configuration (user secret "Spotify:AccessToken"
/// or the SPOTIFY_ACCESS_TOKEN environment variable); it can also be pasted in the UI.
/// </summary>
public sealed class TokenStore(IConfiguration config)
{
    public string? AccessToken { get; set; } =
        new[] { config["Spotify:AccessToken"], config["SPOTIFY_ACCESS_TOKEN"] }
            .FirstOrDefault(t => !string.IsNullOrWhiteSpace(t))?.Trim();

    public bool HasToken => !string.IsNullOrWhiteSpace(AccessToken);
}
