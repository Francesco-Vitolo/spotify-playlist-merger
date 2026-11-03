# spotify-playlist-merger

Local Blazor app (.NET 10) that goes through my Spotify playlists. See [PLAN.md](PLAN.md) for the full plan.

## Current state

- **Log in with Spotify** once; the app keeps the login for up to 180 days and renews the access token automatically
- Loads all playlists **owned by me** automatically after login (followed playlists are listed as skipped)
- Shows every playlist with its songs (expand a playlist to see the table)
- Duplicates = same title and same artist(s) (case-insensitive), so album and single versions count as one song;
  detected within a playlist, across playlists and in Liked Songs
- Loads Liked Songs and checks which playlist songs are already liked (a song counts as liked if any version of it is liked)
- Shows **duplicate liked songs**: songs liked more than once (e.g. album + single), with album, type and date liked
  and an **Unlike** button per version (with confirmation; the last version of a song always stays liked)
- Per playlist: shows the songs not yet liked with checkboxes (all ticked by default),
  **Like N selected**, **Select all/none**, **Ignore playlist**
- **Ignore** playlists you know are done (single playlist, or all fully liked playlists at once):
  ignored playlists are remembered in `ignored-playlists.json` and not loaded anymore; **Include again** undoes it
- Never likes a song twice: songs liked in one playlist disappear from the others
- **Dry run** switch: shows what would be liked without changing anything
- Every like and unlike is logged to `liked-log.tsv` (timestamp, action, URI, song, playlist/album) for review or undo
- **Artists** page: all artists in Liked Songs (with number of liked songs); click an artist to see
  their top 10 songs and like them (single song or all not yet liked). Top songs come from Spotify search
  (the official top-tracks endpoint was removed in Feb 2026), so they follow search relevance, not an
  official ranking, and may include live versions or remixes
- Filter by title/artist, "only not yet liked" (default) and hide duplicates
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
**Log out** deletes it. The like log `liked-log.tsv` is in the same folder.

If you logged in before the like feature existed and get a 403 when liking, log out and in again
so the app gets the library permissions.
