namespace Spotify.Models;

public record SpotifyPlaybackModel(
    string Id,
    string Uri,
    string TrackTitle,
    string ArtistName,
    string AlbumName,
    string ImageUrl,
    bool IsPlaying,
    bool ShuffleOn,
    int ProgressMs,
    int DurationMs,
    string DeviceName)
{
    public static SpotifyPlaybackModel Empty { get; } = new(
        "",
        "",
        "Nothing playing",
        "Open Spotify and start playback",
        "",
        "",
        false,
        false,
        0,
        0,
        "");
}
