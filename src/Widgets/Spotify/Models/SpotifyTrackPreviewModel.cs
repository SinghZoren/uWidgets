namespace Spotify.Models;

public record SpotifyTrackPreviewModel(
    string Id,
    string Title,
    string ArtistName,
    string ImageUrl)
{
    public static SpotifyTrackPreviewModel Empty { get; } = new("", "", "", "");
    public bool HasImage => !string.IsNullOrWhiteSpace(ImageUrl);
}
