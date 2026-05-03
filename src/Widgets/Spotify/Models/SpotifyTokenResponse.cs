namespace Spotify.Models;

public record SpotifyTokenResponse(
    string AccessToken,
    string RefreshToken,
    DateTime ExpiresAtUtc);
