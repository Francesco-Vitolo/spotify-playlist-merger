namespace SpotifyLikeAll.Api;

public interface ISpotifyClient
{
    /// <summary>Maximum number of URIs per /me/library request.</summary>
    const int LibraryBatchSize = 40;

    Task<UserDto> GetCurrentUserAsync(CancellationToken ct = default);

    IAsyncEnumerable<PlaylistDto> GetMyPlaylistsAsync(CancellationToken ct = default);

    IAsyncEnumerable<PlaylistItemDto> GetPlaylistItemsAsync(string playlistId, CancellationToken ct = default);

    /// <summary>GET /me/tracks – all songs in Liked Songs (paged).</summary>
    IAsyncEnumerable<SavedTrackDto> GetSavedTracksAsync(CancellationToken ct = default);

    /// <summary>PUT /me/library – likes the given tracks (max <see cref="LibraryBatchSize"/>).</summary>
    Task SaveAsync(IReadOnlyList<string> uris, CancellationToken ct = default);

    /// <summary>DELETE /me/library – unlikes the given tracks (max <see cref="LibraryBatchSize"/>).</summary>
    Task RemoveAsync(IReadOnlyList<string> uris, CancellationToken ct = default);
}
