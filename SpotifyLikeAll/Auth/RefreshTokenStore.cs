using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;

namespace SpotifyLikeAll.Auth;

/// <summary>
/// Persists the Spotify refresh token on this machine, encrypted with ASP.NET Core Data Protection,
/// so the login survives app restarts (Spotify refresh tokens are valid for 180 days).
/// File: %LOCALAPPDATA%\SpotifyLikeAll\refresh-token.dat (Linux/macOS: ~/.local/share/SpotifyLikeAll).
/// </summary>
public sealed class RefreshTokenStore
{
    private readonly IDataProtector _protector;
    private readonly ILogger<RefreshTokenStore> _logger;
    private readonly string _path;

    public RefreshTokenStore(IDataProtectionProvider dataProtection, ILogger<RefreshTokenStore> logger)
    {
        _protector = dataProtection.CreateProtector("SpotifyLikeAll.RefreshToken.v1");
        _logger = logger;

        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SpotifyLikeAll");
        Directory.CreateDirectory(dir);
        _path = Path.Combine(dir, "refresh-token.dat");
    }

    public string? Load()
    {
        if (!File.Exists(_path))
            return null;

        try
        {
            return _protector.Unprotect(File.ReadAllText(_path));
        }
        catch (Exception ex) when (ex is CryptographicException or FormatException)
        {
            _logger.LogWarning(ex, "Stored refresh token could not be decrypted; a new login is required.");
            Delete();
            return null;
        }
    }

    public void Save(string refreshToken) => File.WriteAllText(_path, _protector.Protect(refreshToken));

    public void Delete()
    {
        if (File.Exists(_path))
            File.Delete(_path);
    }
}
