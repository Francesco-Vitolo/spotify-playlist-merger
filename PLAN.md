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

- .NET 10 **Blazor Web App** (Interactive Server), running locally on `http://127.0.0.1:8888`
- `HttpClient` (typed client) + `System.Text.Json`
- Built-in DI, config (`appsettings.json`, user secrets) and logging

## Project structure

```
SpotifyLikeAll/
  Program.cs                 host setup, DI
  appsettings.json           ClientId, RedirectUri, BatchSize
  Components/
    App.razor, Routes.razor, Layout/MainLayout.razor
    Pages/Home.razor         playlists, songs, duplicates, confirmation UI
  wwwroot/app.css
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
    PlaylistLoader.cs        load owned playlists + songs
    TrackRegistry.cs         dedupe songs across playlists
    LikeAllService.cs        like workflow
```

## Steps

### 1. Spotify app setup (one time)
- [x] Create an app at https://developer.spotify.com/dashboard
- [x] Redirect URI: `http://127.0.0.1:8888/callback`
- [x] Scopes: `playlist-read-private`, `playlist-read-collaborative`, `user-library-read`, `user-library-modify`
- [x] Store Client ID and Client Secret as user secrets (`Spotify:ClientId`, `Spotify:ClientSecret`)

### 2. Authentication (`Auth/`)
- [x] Authorization Code flow with client secret (server-side Blazor, so the secret never reaches the browser)
- [x] `/login` redirects to Spotify with a random `state` (cookie), `/callback` validates it and exchanges the code
- [x] Refresh token stored encrypted with ASP.NET Core Data Protection in `%LOCALAPPDATA%\SpotifyLikeAll`
- [x] Access token refreshed automatically before it expires (and once on an unexpected 401)
- [x] `/logout` deletes the stored refresh token

### 3. API client (`SpotifyClient`)
- [x] `IAsyncEnumerable<Playlist> GetMyPlaylistsAsync()` → `GET /me/playlists` (paged, limit 50)
- [x] `IAsyncEnumerable<PlaylistItem> GetPlaylistItemsAsync(string id)` → `GET /playlists/{id}/items` (paged)
- [x] `Task<bool[]> CheckSavedAsync(IReadOnlyList<string> uris)` → `GET /me/library/contains`
- [x] `Task SaveAsync(IReadOnlyList<string> uris)` → `PUT /me/library`
- [x] Batch with `.Chunk(40)` (max 40 URIs per `/me/library` request)
- [x] Attach the bearer token, refresh on 401, wait for `Retry-After` on 429 (in `SpotifyClient.SendAsync`)

### 4. Workflow (`PlaylistLoader`, `LibraryService`, `Home.razor`)
1. [x] Load all playlists; mark playlists without readable items as *skipped (not owned/collaborative)*
2. [x] For each playlist, load items and keep only real tracks:
   - skip episodes, `is_local` files and null items
   - remove duplicates (same title + artist, case-insensitive)
3. [x] Remove tracks that are already liked:
   - keep a global `HashSet<string>` of known liked URIs across playlists
   - check the rest with `CheckSavedAsync`
4. [x] If nothing is left, print `✔ all liked` and continue with the next playlist
5. [x] Otherwise show the not-yet-liked songs of the playlist in the Blazor UI:
   - table with a checkbox per song, all pre-checked; untick the ones to exclude
   - buttons per playlist: **Like selected** / **Skip**
6. [x] Save the confirmed songs in batches (progress shown in the UI) and add them to the liked set
7. [x] Show a summary: playlists processed, songs liked, playlists skipped (with reason)
8. [x] Show duplicate liked songs (same title + artist liked more than once, e.g. album and single)
9. [x] Unlike extra versions of duplicate liked songs (`DELETE /me/library`, confirmation, last version is kept)

### 5. Safety
- [x] Dry-run switch in the UI: only list songs, never call `PUT /me/library`
- [x] Append every liked URI with a timestamp to `liked-log.tsv` (for undo via `DELETE /me/library`)
- [x] Never like anything without an explicit confirmation per playlist

## Possible later extensions
- Merge several playlists into one new playlist (`POST /me/playlists`, `POST /playlists/{id}/items`)
- Export the not-yet-liked list to CSV
- Resume support (remember processed playlists between runs)

## References
- Migration guide (Feb 2026 Dev Mode changes): https://developer.spotify.com/documentation/web-api/tutorials/february-2026-migration-guide
- Web API reference: https://developer.spotify.com/documentation/web-api
- Authorization Code with PKCE: https://developer.spotify.com/documentation/web-api/tutorials/code-pkce-flow
