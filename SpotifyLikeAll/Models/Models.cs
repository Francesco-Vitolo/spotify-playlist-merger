using SpotifyLikeAll.Api;

namespace SpotifyLikeAll.Models;

public sealed record Track(
    string Uri,
    string Name,
    string Artists,
    string? Isrc = null,
    string? Album = null,
    string? AlbumType = null)
{
    /// <summary>
    /// Key used to detect duplicates: same title and same artist(s), case- and whitespace-insensitive.
    /// So the album version and the single version of a song count as the same song.
    /// </summary>
    public string DedupeKey { get; } = Normalize(Artists) + "|" + Normalize(Name);

    public override string ToString() => $"{Artists} – {Name}";

    public static Track FromDto(TrackDto dto) => new(
        dto.Uri,
        dto.Name,
        string.Join(", ", (dto.Artists ?? []).Select(a => a.Name)),
        dto.ExternalIds?.Isrc,
        dto.Album?.Name,
        dto.Album?.AlbumType);

    private static string Normalize(string value) =>
        string.Join(' ', value.ToLowerInvariant().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
}

public sealed record PlaylistTrackEntry(Track Track, DuplicateKind Duplicate, string? FirstSeenInPlaylist);

public enum DuplicateKind
{
    None,
    SamePlaylist,
    OtherPlaylist,
}

public sealed record OwnedPlaylist(string Id, string Name, IReadOnlyList<PlaylistTrackEntry> Entries, int SkippedNonTracks)
{
    public IEnumerable<Track> UniqueTracks =>
        Entries.Where(e => e.Duplicate == DuplicateKind.None).Select(e => e.Track);
}

public sealed record SkippedPlaylist(string Name, string OwnerName);

/// <summary>A song in the user's Liked Songs.</summary>
public sealed record LikedTrack(Track Track, DateTimeOffset? AddedAt);

/// <summary>The same song (title + artist) liked more than once, e.g. album and single version.</summary>
public sealed record LikedDuplicateGroup(string Title, string Artists, List<LikedTrack> Versions);

/// <summary>An artist credited on at least one liked song.</summary>
public sealed record ArtistSummary(string Id, string Name, int LikedCount);

public sealed record LikedLibrary(
    HashSet<string> Keys,
    List<LikedDuplicateGroup> Duplicates,
    List<ArtistSummary> Artists)
{
    /// <summary>Number of tracks in Liked Songs (updated when the app likes or unlikes tracks).</summary>
    public int Count { get; set; }
}
