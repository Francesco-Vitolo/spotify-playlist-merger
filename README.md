# spotify-playlist-merger

Console app (.NET 10) that goes through my Spotify playlists. See [PLAN.md](PLAN.md) for the full plan.

## Current state

- Loads all playlists **owned by me** (followed playlists are listed as skipped)
- Lists every song per playlist
- Detects duplicates within a playlist and across playlists (by ISRC, falling back to the Spotify URI); `TrackRegistry.UniqueTracks` holds each song exactly once for the later "like" step
- Ignores podcast episodes, local files and unavailable items
- Retries automatically on rate limits (HTTP 429)

## Run

Requires a Spotify user access token with the scopes `playlist-read-private` and `playlist-read-collaborative`
(there is no login flow yet).

```powershell
$env:SPOTIFY_ACCESS_TOKEN = "<token>"
dotnet run --project SpotifyLikeAll
```

```bash
SPOTIFY_ACCESS_TOKEN="<token>" dotnet run --project SpotifyLikeAll
```
