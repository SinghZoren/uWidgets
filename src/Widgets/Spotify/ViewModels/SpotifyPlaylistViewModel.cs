using Spotify.Models;

namespace Spotify.ViewModels;

public class SpotifyPlaylistViewModel(SpotifyPlaylistModel model)
{
    public string Id => model.Id;
    public string Name => model.Name;
    public string ImageUrl => model.ImageUrl;
    public string TrackCountText => model.TrackCountText;
    public string OwnerName => model.OwnerName;
    public string Uri => $"spotify:playlist:{model.Id}";
}
