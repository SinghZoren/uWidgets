namespace Spotify.Models;

public record SpotifyPlaylistModel(
    string Id,
    string Name,
    string ImageUrl,
    int TrackCount,
    string OwnerName)
{
    public string TrackCountText => TrackCount == 1 ? "1 track" : $"{TrackCount} tracks";
}
