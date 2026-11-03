using System.Text;
using SpotifyLikeAll.Api;
using SpotifyLikeAll.Models;
using SpotifyLikeAll.Services;

Console.OutputEncoding = Encoding.UTF8;

var token = Environment.GetEnvironmentVariable("SPOTIFY_ACCESS_TOKEN");
if (string.IsNullOrWhiteSpace(token))
{
    Console.Error.WriteLine("Set the SPOTIFY_ACCESS_TOKEN environment variable to a Spotify user access token");
    Console.Error.WriteLine("with the scopes playlist-read-private and playlist-read-collaborative.");
    return 1;
}

var client = SpotifyClient.Create(token.Trim());
var registry = new TrackRegistry();
var loader = new PlaylistLoader(client, registry);

PlaylistLoader.Result result;
try
{
    result = await loader.LoadOwnedPlaylistsAsync();
}
catch (SpotifyApiException ex)
{
    Console.Error.WriteLine($"Spotify API error: {ex.Message}");
    return 1;
}

Console.WriteLine($"Logged in as {result.Me.DisplayName ?? result.Me.Id}");
Console.WriteLine($"{result.Owned.Count} playlists owned by you, {result.NotOwned.Count} followed playlists skipped.");
Console.WriteLine();

foreach (var playlist in result.Owned)
{
    Console.WriteLine($"== {playlist.Name} ({playlist.Entries.Count} songs) ==");

    var number = 1;
    foreach (var entry in playlist.Entries)
    {
        var note = entry.Duplicate switch
        {
            DuplicateKind.SamePlaylist => "  [duplicate in this playlist]",
            DuplicateKind.OtherPlaylist => $"  [duplicate, already in \"{entry.FirstSeenInPlaylist}\"]",
            _ => "",
        };
        Console.WriteLine($"{number,4}. {entry.Track}{note}");
        number++;
    }

    if (playlist.SkippedNonTracks > 0)
        Console.WriteLine($"      ({playlist.SkippedNonTracks} episodes/local files/unavailable items ignored)");

    Console.WriteLine();
}

if (result.NotOwned.Count > 0)
{
    Console.WriteLine("Skipped (not owned by you):");
    foreach (var p in result.NotOwned)
        Console.WriteLine($"  - {p.Name} (by {p.OwnerName})");
    Console.WriteLine();
}

var totalEntries = result.Owned.Sum(p => p.Entries.Count);
Console.WriteLine($"Total: {totalEntries} songs, {registry.Count} unique, {totalEntries - registry.Count} duplicates.");
return 0;
