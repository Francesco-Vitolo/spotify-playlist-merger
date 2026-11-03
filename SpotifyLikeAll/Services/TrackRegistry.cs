using SpotifyLikeAll.Models;

namespace SpotifyLikeAll.Services;

/// <summary>
/// Remembers every song seen across all playlists so each song is handled only once.
/// The later "like" step should work from <see cref="UniqueTracks"/> only.
/// </summary>
public sealed class TrackRegistry
{
    private readonly Dictionary<string, (Track Track, string PlaylistId, string PlaylistName)> _seen = new();

    public IReadOnlyCollection<Track> UniqueTracks => _seen.Values.Select(v => v.Track).ToList();

    public int Count => _seen.Count;

    public PlaylistTrackEntry Register(Track track, string playlistId, string playlistName)
    {
        if (_seen.TryGetValue(track.DedupeKey, out var first))
        {
            var kind = first.PlaylistId == playlistId ? DuplicateKind.SamePlaylist : DuplicateKind.OtherPlaylist;
            return new PlaylistTrackEntry(track, kind, first.PlaylistName);
        }

        _seen[track.DedupeKey] = (track, playlistId, playlistName);
        return new PlaylistTrackEntry(track, DuplicateKind.None, null);
    }
}
