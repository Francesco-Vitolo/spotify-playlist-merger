using SpotifyLikeAll.Api;
using SpotifyLikeAll.Models;

namespace SpotifyLikeAll.Services;

/// <summary>
/// Approximates an artist's top songs with Spotify search (the official top-tracks endpoint was removed
/// in Feb 2026). Results follow Spotify's search relevance, not an official popularity ranking.
/// </summary>
public sealed class TopTracksService(ISpotifyClient client, AppState state)
{
    public const int TopCount = 10;
    // Search pages can be small (Spotify may return 5 instead of 10) and many results are filtered out
    // (features, repeated titles), so keep paging until 10 songs are found or results run out.
    private const int MaxSearchRequests = 8;

    public async Task<IReadOnlyList<Track>> GetTopTracksAsync(ArtistSummary artist, CancellationToken ct = default)
    {
        if (state.TopTracks.TryGetValue(artist.Id, out var cached))
            return cached;

        var query = $"artist:\"{artist.Name.Replace("\"", "")}\"";
        var result = new List<Track>();
        var seen = new HashSet<string>();

        var offset = 0;
        for (var request = 0; request < MaxSearchRequests && result.Count < TopCount; request++)
        {
            var items = await client.SearchTracksAsync(query, offset, ct);
            if (items.Count == 0)
                break; // no more results

            offset += items.Count;

            foreach (var dto in items)
            {
                // Only songs where this artist is the main artist (skips guest features of other artists).
                if (dto.IsLocal || string.IsNullOrEmpty(dto.Uri) ||
                    dto.Artists is not { Count: > 0 } || dto.Artists[0].Id != artist.Id)
                {
                    continue;
                }

                // One entry per song: album and single version of the same title count once.
                var track = Track.FromDto(dto);
                if (seen.Add(track.DedupeKey))
                    result.Add(track);

                if (result.Count == TopCount)
                    break;
            }
        }

        state.TopTracks[artist.Id] = result;
        return result;
    }
}
