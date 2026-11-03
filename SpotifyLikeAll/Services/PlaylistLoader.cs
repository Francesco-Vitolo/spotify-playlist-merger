using SpotifyLikeAll.Api;
using SpotifyLikeAll.Models;

namespace SpotifyLikeAll.Services;

public sealed class PlaylistLoader(ISpotifyClient client, TrackRegistry registry)
{
    public sealed record Result(
        UserDto Me,
        IReadOnlyList<OwnedPlaylist> Owned,
        IReadOnlyList<SkippedPlaylist> NotOwned);

    /// <summary>Loads all playlists owned by the current user together with their songs.</summary>
    public async Task<Result> LoadOwnedPlaylistsAsync(CancellationToken ct = default)
    {
        var me = await client.GetCurrentUserAsync(ct);

        var owned = new List<OwnedPlaylist>();
        var notOwned = new List<SkippedPlaylist>();

        await foreach (var playlist in client.GetMyPlaylistsAsync(ct))
        {
            if (playlist.Owner?.Id != me.Id)
            {
                notOwned.Add(new SkippedPlaylist(playlist.Name, playlist.Owner?.DisplayName ?? playlist.Owner?.Id ?? "?"));
                continue;
            }

            owned.Add(await LoadPlaylistAsync(playlist, ct));
        }

        return new Result(me, owned, notOwned);
    }

    private async Task<OwnedPlaylist> LoadPlaylistAsync(PlaylistDto playlist, CancellationToken ct)
    {
        var entries = new List<PlaylistTrackEntry>();
        var skipped = 0;

        await foreach (var item in client.GetPlaylistItemsAsync(playlist.Id, ct))
        {
            var content = item.Content;

            // Only real Spotify tracks: no podcast episodes, no local files, no removed/unavailable items.
            if (content is null || item.IsLocal || content.IsLocal || content.Type != "track" ||
                string.IsNullOrEmpty(content.Uri))
            {
                skipped++;
                continue;
            }

            var track = new Track(
                content.Uri,
                content.Name,
                string.Join(", ", (content.Artists ?? []).Select(a => a.Name)),
                content.ExternalIds?.Isrc);

            entries.Add(registry.Register(track, playlist.Id, playlist.Name));
        }

        return new OwnedPlaylist(playlist.Id, playlist.Name, entries, skipped);
    }
}
