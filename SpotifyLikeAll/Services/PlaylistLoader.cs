using SpotifyLikeAll.Api;
using SpotifyLikeAll.Models;

namespace SpotifyLikeAll.Services;

public sealed class PlaylistLoader(ISpotifyClient client, IgnoredPlaylistsStore ignoredStore)
{
    public sealed record Result(
        UserDto Me,
        IReadOnlyList<OwnedPlaylist> Owned,
        IReadOnlyList<SkippedPlaylist> NotOwned,
        IReadOnlyList<IgnoredPlaylist> Ignored,
        TrackRegistry Registry);

    /// <summary>
    /// Loads all playlists owned by the current user together with their songs.
    /// Every song is registered in a fresh <see cref="TrackRegistry"/>, so duplicates
    /// within and across playlists are marked and each song exists only once in
    /// <see cref="TrackRegistry.UniqueTracks"/>.
    /// </summary>
    public async Task<Result> LoadOwnedPlaylistsAsync(Action<string>? onProgress = null, CancellationToken ct = default)
    {
        var registry = new TrackRegistry();
        var me = await client.GetCurrentUserAsync(ct);

        var owned = new List<OwnedPlaylist>();
        var notOwned = new List<SkippedPlaylist>();
        var ignored = new List<IgnoredPlaylist>();

        await foreach (var playlist in client.GetMyPlaylistsAsync(ct))
        {
            if (playlist.Owner?.Id != me.Id)
            {
                notOwned.Add(new SkippedPlaylist(playlist.Name, playlist.Owner?.DisplayName ?? playlist.Owner?.Id ?? "?"));
                continue;
            }

            if (ignoredStore.IsIgnored(playlist.Id))
            {
                // Not loaded at all: saves API calls for playlists the user marked as done.
                ignored.Add(new IgnoredPlaylist(playlist.Id, playlist.Name, playlist.ItemCount));
                continue;
            }

            onProgress?.Invoke($"Loading \"{playlist.Name}\" ({owned.Count + 1}) …");
            owned.Add(await LoadPlaylistAsync(playlist, registry, ct));
        }

        return new Result(me, owned, notOwned, ignored, registry);
    }

    private async Task<OwnedPlaylist> LoadPlaylistAsync(PlaylistDto playlist, TrackRegistry registry, CancellationToken ct)
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

            entries.Add(registry.Register(Track.FromDto(content), playlist.Id, playlist.Name));
        }

        return new OwnedPlaylist(playlist.Id, playlist.Name, entries, skipped);
    }
}
