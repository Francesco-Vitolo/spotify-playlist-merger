using SpotifyLikeAll.Api;
using SpotifyLikeAll.Models;

namespace SpotifyLikeAll.Services;

/// <summary>Checks which songs are already liked and likes new ones without ever liking a song twice.</summary>
public sealed class LibraryService(ISpotifyClient client, LikedLog log)
{
    private const int BatchSize = ISpotifyClient.LibraryBatchSize;

    /// <summary>
    /// Returns the dedupe keys of all songs that are already liked. Every URI is checked, including
    /// other versions of the same recording, so a song counts as liked if any of its versions is liked.
    /// </summary>
    public async Task<HashSet<string>> GetLikedKeysAsync(
        IEnumerable<OwnedPlaylist> playlists, Action<string>? onProgress = null, CancellationToken ct = default)
    {
        var tracks = playlists
            .SelectMany(p => p.Entries)
            .Select(e => e.Track)
            .DistinctBy(t => t.Uri)
            .ToList();

        var liked = new HashSet<string>();
        var checkedCount = 0;

        foreach (var batch in tracks.Chunk(BatchSize))
        {
            var flags = await client.CheckSavedAsync(batch.Select(t => t.Uri).ToList(), ct);
            for (var i = 0; i < batch.Length && i < flags.Length; i++)
            {
                if (flags[i])
                    liked.Add(batch[i].DedupeKey);
            }

            checkedCount += batch.Length;
            onProgress?.Invoke($"Checking liked songs … {checkedCount}/{tracks.Count}");
        }

        return liked;
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

            await log.AppendAsync(batch, playlistName, ct);

            likedCount += batch.Length;
            onProgress?.Invoke($"Liking songs … {likedCount}/{toLike.Count}");
        }

        return likedCount;
    }
}
