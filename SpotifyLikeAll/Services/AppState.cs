using System.Collections.Concurrent;
using SpotifyLikeAll.Models;

namespace SpotifyLikeAll.Services;

/// <summary>
/// In-memory cache shared by all pages (single-user local app → singleton), so switching between the
/// Playlists and Artists tabs does not load everything from Spotify again. "Reload" refreshes it.
/// The liked-song keys are one shared set, so likes made on one page are visible on the other.
/// </summary>
public sealed class AppState
{
    public PlaylistLoader.Result? Playlists { get; set; }

    public LikedLibrary? Library { get; set; }

    /// <summary>Search-based top songs per artist id.</summary>
    public ConcurrentDictionary<string, IReadOnlyList<Track>> TopTracks { get; } = new();

    public void Clear()
    {
        Playlists = null;
        Library = null;
        TopTracks.Clear();
    }
}
