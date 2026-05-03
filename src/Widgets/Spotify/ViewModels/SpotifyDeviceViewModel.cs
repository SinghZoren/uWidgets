using Spotify.Models;

namespace Spotify.ViewModels;

public class SpotifyDeviceViewModel(SpotifyDeviceModel model)
{
    public string Id => model.Id;
    public string Name => model.Name;
    public string Type => model.Type;
    public bool IsActive => model.IsActive;
    public bool IsRestricted => model.IsRestricted;
    public string DisplayName => model.DisplayName;
}
