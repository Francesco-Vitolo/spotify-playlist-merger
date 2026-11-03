using SpotifyLikeAll.Api;
using SpotifyLikeAll.Models;

namespace SpotifyLikeAll.Services;

/// <summary>Reads Liked Songs and likes new songs without ever liking a song (title + artist) twice.</summary>
public sealed class LibraryService(ISpotifyClient client, LikedLog log)
{
    private const int BatchSize = ISpotifyClient.LibraryBatchSize;

    /// <summary>
    /// Loads all Liked Songs. Returns the dedupe keys (title + artist) of every liked song, so a playlist
    /// song counts as liked if any version of it is liked, plus the songs that are liked more than once.
    /// </summary>
    public async Task<LikedLibrary> LoadLikedLibraryAsync(Action<string>? onProgress = null, CancellationToken ct = default)
    {
        var liked = new List<LikedTrack>();
        var artistCounts = new Dictionary<string, (string Name, int Count)>();

        await foreach (var saved in client.GetSavedTracksAsync(ct))
        {
            var content = saved.Content;
            if (content is null || content.IsLocal || string.IsNullOrEmpty(content.Uri) ||
                (content.Type.Length > 0 && content.Type != "track"))
            {
                continue;
            }

            liked.Add(new LikedTrack(Track.FromDto(content), saved.AddedAt));

            foreach (var artist in content.Artists ?? [])
            {
                if (string.IsNullOrEmpty(artist.Id))
                    continue;

                artistCounts[artist.Id] = artistCounts.TryGetValue(artist.Id, out var entry)
                    ? (entry.Name, entry.Count + 1)
                    : (artist.Name, 1);
            }

            if (liked.Count % 100 == 0)
                onProgress?.Invoke($"Loading liked songs … {liked.Count}");
        }

        var keys = liked.Select(l => l.Track.DedupeKey).ToHashSet();

        var duplicates = liked
            .GroupBy(l => l.Track.DedupeKey)
            .Where(g => g.Select(l => l.Track.Uri).Distinct().Count() > 1)
            .Select(g =>
            {
                var versions = g.OrderBy(l => l.AddedAt).ToList();
                return new LikedDuplicateGroup(versions[0].Track.Name, versions[0].Track.Artists, versions);
            })
            .OrderBy(g => g.Artists, StringComparer.OrdinalIgnoreCase)
            .ThenBy(g => g.Title, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var artists = artistCounts
            .Select(a => new ArtistSummary(a.Key, a.Value.Name, a.Value.Count))
            .OrderByDescending(a => a.LikedCount)
            .ThenBy(a => a.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new LikedLibrary(keys, duplicates, artists) { Count = liked.Count };
    }

    /// <summary>
    /// Likes the given tracks in batches. Songs whose key is already in <paramref name="likedKeys"/> and
    /// duplicates within the list are skipped; successfully liked keys are added to <paramref name="likedKeys"/>.
    /// </summary>
    /// <returns>Number of songs liked.</returns>
    public async Task<int> LikeAsync(
        IEnumerable<Track> tracks,
        string playlistName,
        ISet<string> likedKeys,
        Action<string>? onProgress = null,
        CancellationToken ct = default)
    {
        var toLike = tracks
            .Where(t => !likedKeys.Contains(t.DedupeKey))
            .DistinctBy(t => t.DedupeKey)
            .ToList();

        var likedCount = 0;
        foreach (var batch in toLike.Chunk(BatchSize))
        {
            await client.SaveAsync(batch.Select(t => t.Uri).ToList(), ct);

            foreach (var track in batch)
                likedKeys.Add(track.DedupeKey);

            await log.AppendAsync(batch, "liked", playlistName, ct);

            likedCount += batch.Length;
            onProgress?.Invoke($"Liking songs … {likedCount}/{toLike.Count}");
        }

        return likedCount;
    }

    /// <summary>
    /// Unlikes one version of a song that is liked more than once. Refuses to remove the last
    /// remaining version, so the song itself always stays in Liked Songs.
    /// </summary>
    public async Task UnlikeDuplicateAsync(
        LikedLibrary library, LikedDuplicateGroup group, LikedTrack version, CancellationToken ct = default)
    {
        if (group.Versions.Count < 2 || !group.Versions.Contains(version))
            throw new InvalidOperationException("Only extra versions of a song liked more than once can be unliked.");

        await client.RemoveAsync([version.Track.Uri], ct);
        await log.AppendAsync([version.Track], "unliked", version.Track.Album ?? "", ct);

        group.Versions.Remove(version);
        library.Count--;

        if (group.Versions.Count < 2)
            library.Duplicates.Remove(group);
    }
}
