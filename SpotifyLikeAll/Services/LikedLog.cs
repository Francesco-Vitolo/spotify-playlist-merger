using SpotifyLikeAll.Models;

namespace SpotifyLikeAll.Services;

/// <summary>
/// Appends every liked song to liked-log.tsv in the app data folder, so likes can be reviewed
/// or undone later (DELETE /me/library with the logged URIs).
/// Columns: timestamp (UTC), Spotify URI, song, playlist.
/// </summary>
public sealed class LikedLog
{
    private readonly SemaphoreSlim _lock = new(1, 1);

    public string FilePath { get; } = Path.Combine(AppPaths.DataDirectory, "liked-log.tsv");

    public async Task AppendAsync(IEnumerable<Track> tracks, string playlistName, CancellationToken ct = default)
    {
        var timestamp = DateTimeOffset.UtcNow.ToString("u");
        var lines = tracks.Select(t => string.Join('\t', timestamp, t.Uri, Clean(t.ToString()), Clean(playlistName)));

        await _lock.WaitAsync(ct);
        try
        {
            await File.AppendAllLinesAsync(FilePath, lines, ct);
        }
        finally
        {
            _lock.Release();
        }
    }

    private static string Clean(string value) => value.Replace('\t', ' ').Replace('\n', ' ').Replace('\r', ' ');
}
