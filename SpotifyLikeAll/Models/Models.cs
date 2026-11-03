namespace SpotifyLikeAll.Models;

public sealed record Track(string Uri, string Name, string Artists, string? Isrc)
{
    /// <summary>
    /// Key used to detect duplicates. The ISRC identifies a recording, so the same song
    /// released on a single and an album counts as one song. Falls back to the URI.
    /// </summary>
    public string DedupeKey => string.IsNullOrWhiteSpace(Isrc) ? Uri : "isrc:" + Isrc.Trim().ToUpperInvariant();

    public override string ToString() => $"{Artists} – {Name}";
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
