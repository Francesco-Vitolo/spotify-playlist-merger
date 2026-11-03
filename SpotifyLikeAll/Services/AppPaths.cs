namespace SpotifyLikeAll.Services;

public static class AppPaths
{
    /// <summary>%LOCALAPPDATA%\SpotifyLikeAll (Linux/macOS: ~/.local/share/SpotifyLikeAll).</summary>
    public static string DataDirectory { get; } = CreateDataDirectory();

    private static string CreateDataDirectory()
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SpotifyLikeAll");
        Directory.CreateDirectory(dir);
        return dir;
    }
}
