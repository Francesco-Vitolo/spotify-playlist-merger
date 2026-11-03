using SpotifyLikeAll.Models;

namespace SpotifyLikeAll.Services;

/// <summary>
/// Appends every song the app likes or unlikes to liked-log.tsv in the app data folder, so changes
/// can be reviewed or undone later.
/// Columns: timestamp (UTC), action (liked/unliked), Spotify URI, song, context (playlist or album).
/// </summary>
public sealed class LikedLog
{
    private readonly SemaphoreSlim _lock = new(1, 1);

    public string FilePath { get; } = Path.Combine(AppPaths.DataDirectory, "liked-log.tsv");

    public async Task AppendAsync(
        IEnumerable<Track> tracks, string action, string context, CancellationToken ct = default)
    {
        var timestamp = DateTimeOffset.UtcNow.ToString("u");
        var lines = tracks.Select(t =>
            string.Join('\t', timestamp, action, t.Uri, Clean(t.ToString()), Clean(context)));

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
