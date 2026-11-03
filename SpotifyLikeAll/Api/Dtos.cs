namespace SpotifyLikeAll.Api;

// Raw Spotify Web API response shapes (JSON is snake_case, mapped via JsonNamingPolicy.SnakeCaseLower).

public sealed class PagingDto<T>
{
    public List<T>? Items { get; init; }
    public string? Next { get; init; }
    public int Total { get; init; }
}

public sealed class UserDto
{
    public string Id { get; init; } = "";
    public string? DisplayName { get; init; }
}

public sealed class PlaylistDto
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public UserDto? Owner { get; init; }
    public bool Collaborative { get; init; }

    // Since Feb 2026 the item reference is called "items"; older responses used "tracks".
    public ItemsRefDto? Items { get; init; }
    public ItemsRefDto? Tracks { get; init; }

    public int ItemCount => Items?.Total ?? Tracks?.Total ?? 0;
}

public sealed class ItemsRefDto
{
    public string? Href { get; init; }
    public int Total { get; init; }
}

public sealed class PlaylistItemDto
{
    // Since Feb 2026 the content is called "item"; older responses used "track".
    public TrackDto? Item { get; init; }
    public TrackDto? Track { get; init; }
    public bool IsLocal { get; init; }

    public TrackDto? Content => Item ?? Track;
}

public sealed class TrackDto
{
    public string? Id { get; init; }
    public string Uri { get; init; } = "";
    public string Name { get; init; } = "";
    public string Type { get; init; } = "";
    public bool IsLocal { get; init; }
    public List<ArtistDto>? Artists { get; init; }
    public ExternalIdsDto? ExternalIds { get; init; }
}

public sealed class ArtistDto
{
    public string Name { get; init; } = "";
}

public sealed class ExternalIdsDto
{
    public string? Isrc { get; init; }
}
