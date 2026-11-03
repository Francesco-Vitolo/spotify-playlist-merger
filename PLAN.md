# Spotify "Like All" – Implementation Plan (C#)

Goal: go through all of my Spotify playlists, show the songs in each playlist that are **not yet liked**, and like them only after I confirm.

## Spotify API constraints (as of 2026)

- **Premium required:** Development Mode apps only work while the app owner has an active Spotify Premium subscription.
- **5 users max** per new Development Mode app (fine for personal use).
- **Only own/collaborative playlists are readable:** `GET /playlists/{id}/items` returns items only for playlists I own or collaborate on. Followed playlists return metadata only → report them as *skipped*.
- **New endpoints (old ones return 403/are removed):**

  | Purpose | Use | Removed |
  |---|---|---|
  | Playlist tracks | `GET /playlists/{id}/items` | `GET /playlists/{id}/tracks` |
  | Like songs | `PUT /me/library` (Spotify URIs) | `PUT /me/tracks` |
  | Check if liked | `GET /me/library/contains` (URIs) | `GET /me/tracks/contains` |
  | Unlike (undo) | `DELETE /me/library` | `DELETE /me/tracks` |

- Avoid wrapper libraries (e.g. SpotifyAPI-NET) unless they are confirmed to use the 2026 endpoints.

## Tech stack

- .NET 10 console app
- `HttpClient` (typed client) + `System.Text.Json`
- `Microsoft.Extensions.Hosting` for DI, config (`appsettings.json`) and logging
- `Spectre.Console` for tables and prompts (optional)

## Project structure

```
SpotifyLikeAll/
  Program.cs                 host setup, CLI args (--dry-run)
  appsettings.json           ClientId, RedirectUri, BatchSize
  Auth/
    AuthService.cs           PKCE login flow
    PkceHelper.cs            code verifier / challenge
    TokenStore.cs            persist refresh token
  Api/
    ISpotifyClient.cs
    SpotifyClient.cs         endpoints, paging, batching
    SpotifyAuthHandler.cs    DelegatingHandler: bearer token, 401 refresh, 429 retry
  Models/
    Playlist.cs, Track.cs, PlaylistItem.cs, Paging<T>.cs   (records)
  Services/
    LikeAllService.cs        main workflow
    ConsolePrompter.cs       confirmation UI
```

## Steps

### 1. Spotify app setup (one time)
- [ ] Create an app at https://developer.spotify.com/dashboard
- [ ] Redirect URI: `http://127.0.0.1:8888/callback`
- [ ] Scopes: `playlist-read-private`, `playlist-read-collaborative`, `user-library-read`, `user-library-modify`
- [ ] Put the Client ID into `appsettings.json` (no client secret needed with PKCE)

### 2. Authentication (`AuthService`)
- [ ] Generate code verifier (`RandomNumberGenerator`) and challenge (SHA-256, Base64Url)
- [ ] Open the authorize URL in the browser (`Process.Start` with `UseShellExecute = true`)
- [ ] Receive the callback with `HttpListener` on `http://127.0.0.1:8888/`
- [ ] Exchange the code at `https://accounts.spotify.com/api/token`
- [ ] Store the refresh token (JSON in `%APPDATA%`, optionally protected with `ProtectedData`)
- [ ] Refresh the access token automatically (it expires after 1 hour)

### 3. API client (`SpotifyClient`)
- [ ] `IAsyncEnumerable<Playlist> GetMyPlaylistsAsync()` → `GET /me/playlists` (paged, limit 50)
- [ ] `IAsyncEnumerable<PlaylistItem> GetPlaylistItemsAsync(string id)` → `GET /playlists/{id}/items` (paged)
- [ ] `Task<bool[]> CheckSavedAsync(IReadOnlyList<string> uris)` → `GET /me/library/contains`
- [ ] `Task SaveAsync(IReadOnlyList<string> uris)` → `PUT /me/library`
- [ ] Batch with `.Chunk(BatchSize)` (check the API reference for the current max; the old endpoint allowed 50)
- [ ] `SpotifyAuthHandler`: attach the bearer token, refresh on 401, wait for `Retry-After` on 429

### 4. Workflow (`LikeAllService`)
1. [ ] Load all playlists; mark playlists without readable items as *skipped (not owned/collaborative)*
2. [ ] For each playlist, load items and keep only real tracks:
   - skip episodes, `is_local` files and null items
   - remove duplicates with `DistinctBy(t => t.Uri)`
3. [ ] Remove tracks that are already liked:
   - keep a global `HashSet<string>` of known liked URIs across playlists
   - check the rest with `CheckSavedAsync`
4. [ ] If nothing is left, print `✔ all liked` and continue with the next playlist
5. [ ] Otherwise show the not-yet-liked songs and ask for confirmation:
   ```
   Playlist "Road Trip" – 12 of 48 songs not yet liked
    #  Artist           Title
    1  ...              ...
   Like these 12 songs? [y]es / [n]o-skip / [s]elect / [q]uit
   ```
   - `y` → like all listed songs
   - `n` → skip this playlist
   - `s` → `MultiSelectionPrompt` with all songs pre-selected; untick the ones to exclude
   - `q` → stop and show the summary
6. [ ] Save the confirmed songs in batches and add them to the liked set
7. [ ] Print a summary: playlists processed, songs liked, playlists skipped (with reason)

### 5. Safety
- [ ] `--dry-run`: only list songs, never call `PUT /me/library`
- [ ] Append every liked URI with a timestamp to `liked-log.txt` (for undo via `DELETE /me/library`)
- [ ] Never like anything without an explicit confirmation per playlist

## Possible later extensions
- Merge several playlists into one new playlist (`POST /me/playlists`, `POST /playlists/{id}/items`)
- Export the not-yet-liked list to CSV
- Resume support (remember processed playlists between runs)

## References
- Migration guide (Feb 2026 Dev Mode changes): https://developer.spotify.com/documentation/web-api/tutorials/february-2026-migration-guide
- Web API reference: https://developer.spotify.com/documentation/web-api
- Authorization Code with PKCE: https://developer.spotify.com/documentation/web-api/tutorials/code-pkce-flow
