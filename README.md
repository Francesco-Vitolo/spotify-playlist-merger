# spotify-playlist-merger

Local Blazor app (.NET 10) that goes through my Spotify playlists. See [PLAN.md](PLAN.md) for the full plan.

## Current state

- **Log in with Spotify** once; the app keeps the login for up to 180 days and renews the access token automatically
- Loads all playlists **owned by me** automatically after login (followed playlists are listed as skipped)
- Shows every playlist with its songs (expand a playlist to see the table)
- Detects duplicates within a playlist and across playlists (by ISRC, falling back to the Spotify URI);
  `TrackRegistry.UniqueTracks` holds each song exactly once for the later "like" step
- Filter by title/artist and hide duplicates
- Ignores podcast episodes, local files and unavailable items
- Retries automatically on rate limits (HTTP 429)

## Setup (one time)

1. In the [Spotify dashboard](https://developer.spotify.com/dashboard) app settings:
   - API: **Web API**
   - Redirect URI: `http://127.0.0.1:8888/callback`
   - Under **User Management**, add the email of your Spotify account
2. Store the credentials as user secrets (they stay out of the repo):

   ```bash
   dotnet user-secrets set "Spotify:ClientId" "<client id>" --project SpotifyLikeAll
   dotnet user-secrets set "Spotify:ClientSecret" "<client secret>" --project SpotifyLikeAll
   ```

## Run

```bash
dotnet run --project SpotifyLikeAll
```

Open <http://127.0.0.1:8888> (use `127.0.0.1`, not `localhost`, so the login cookie matches the redirect URI)
and click **Log in with Spotify**.

The refresh token is stored encrypted (ASP.NET Core Data Protection) in
`%LOCALAPPDATA%\SpotifyLikeAll\refresh-token.dat` (Linux/macOS: `~/.local/share/SpotifyLikeAll/`).
**Log out** deletes it.
