namespace Spotify.Models;

public record SpotifyModel(
    string ClientId = "",
    string RedirectUri = "http://127.0.0.1:55432/callback/",
    string AccessToken = "",
    string RefreshToken = "",
    DateTime? ExpiresAtUtc = null,
    string SelectedPlaylistId = "",
    string SelectedPlaylistName = "",
    string SelectedDeviceId = "",
    string SelectedDeviceName = "")
{
    public bool IsConfigured => !string.IsNullOrWhiteSpace(ClientId);
    public bool IsConnected => !string.IsNullOrWhiteSpace(RefreshToken);

    public SpotifyModel Normalize()
    {
        var redirectUri = string.IsNullOrWhiteSpace(RedirectUri)
            ? "http://127.0.0.1:55432/callback/"
            : RedirectUri.Trim();

        if (!redirectUri.EndsWith('/'))
            redirectUri += "/";

        return this with
        {
            ClientId = ClientId.Trim(),
            RedirectUri = redirectUri,
            AccessToken = AccessToken.Trim(),
            RefreshToken = RefreshToken.Trim(),
            SelectedPlaylistId = SelectedPlaylistId.Trim(),
            SelectedPlaylistName = SelectedPlaylistName.Trim(),
            SelectedDeviceId = SelectedDeviceId.Trim(),
            SelectedDeviceName = SelectedDeviceName.Trim()
        };
    }
}
