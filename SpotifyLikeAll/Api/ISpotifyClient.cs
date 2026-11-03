namespace SpotifyLikeAll.Api;

public interface ISpotifyClient
{
    Task<UserDto> GetCurrentUserAsync(CancellationToken ct = default);

    IAsyncEnumerable<PlaylistDto> GetMyPlaylistsAsync(CancellationToken ct = default);

    IAsyncEnumerable<PlaylistItemDto> GetPlaylistItemsAsync(string playlistId, CancellationToken ct = default);
}
