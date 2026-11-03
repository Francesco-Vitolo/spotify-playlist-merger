# spotify-playlist-merger

Local Blazor app (.NET 10) that goes through my Spotify playlists. See [PLAN.md](PLAN.md) for the full plan.

## Current state

- Loads all playlists **owned by me** (followed playlists are listed as skipped)
- Shows every playlist with its songs (expand a playlist to see the table)
- Detects duplicates within a playlist and across playlists (by ISRC, falling back to the Spotify URI);
  `TrackRegistry.UniqueTracks` holds each song exactly once for the later "like" step
- Filter by title/artist and hide duplicates
- Ignores podcast episodes, local files and unavailable items
- Retries automatically on rate limits (HTTP 429)

## Run

```bash
dotnet run --project SpotifyLikeAll
```

The app opens on <http://127.0.0.1:8888>.

There is no login flow yet. The app needs a Spotify user access token with the scopes
`playlist-read-private` and `playlist-read-collaborative`. Either paste it into the page, or set it beforehand:

```bash
# user secret (stays out of the repo)
dotnet user-secrets set "Spotify:AccessToken" "<token>" --project SpotifyLikeAll

# or environment variable
export SPOTIFY_ACCESS_TOKEN="<token>"
```

Tokens expire after 1 hour; the page asks for a new one when that happens.
