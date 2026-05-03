namespace Spotify.Models;

public record SpotifyDeviceModel(
    string Id,
    string Name,
    string Type,
    bool IsActive,
    bool IsRestricted)
{
    public string DisplayName => string.IsNullOrWhiteSpace(Type) ? Name : $"{Name} ({Type})";
}
