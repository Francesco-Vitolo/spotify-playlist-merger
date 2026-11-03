using System.Text.Json;

namespace SpotifyLikeAll.Services;

/// <summary>
/// Playlists the user chose to ignore (e.g. because all songs are already liked). Ignored playlists are
/// not loaded at all, which saves API calls. Persisted in ignored-playlists.json in the app data folder.
/// </summary>
public sealed class IgnoredPlaylistsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly object _lock = new();
    private readonly string _path = Path.Combine(AppPaths.DataDirectory, "ignored-playlists.json");
    private readonly Dictionary<string, string> _ignored; // playlist id -> name

    public IgnoredPlaylistsStore(ILogger<IgnoredPlaylistsStore> logger)
    {
        _ignored = new Dictionary<string, string>();
        if (!File.Exists(_path))
            return;

        try
        {
            _ignored = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(_path)) ?? new();
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "Could not read {Path}; starting with no ignored playlists.", _path);
        }
    }

    public bool IsIgnored(string playlistId)
    {
        lock (_lock)
            return _ignored.ContainsKey(playlistId);
    }

    public void Ignore(string playlistId, string name)
    {
        lock (_lock)
        {
            _ignored[playlistId] = name;
            Save();
        }
    }

    public void Include(string playlistId)
    {
        lock (_lock)
        {
            if (_ignored.Remove(playlistId))
                Save();
        }
    }

    private void Save() => File.WriteAllText(_path, JsonSerializer.Serialize(_ignored, JsonOptions));
}
