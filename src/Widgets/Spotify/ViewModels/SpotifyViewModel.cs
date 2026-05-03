using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Text.Json;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using ReactiveUI;
using Spotify.Models;
using Spotify.Services;
using uWidgets.Core.Interfaces;
using uWidgets.Services;

namespace Spotify.ViewModels;

public enum SpotifyWidgetViewMode
{
    Compact,
    Full
}

public class SpotifyViewModel : ReactiveObject, IDisposable
{
    private static readonly HttpClient ImageHttp = new();
    private readonly IWidgetLayoutProvider widgetLayoutProvider;
    private readonly UpdateTimer timer = TimerService.Timer1Second;
    private SpotifyModel model;
    private SpotifyPlaybackModel playback = SpotifyPlaybackModel.Empty;
    private SpotifyWidgetViewMode viewMode = SpotifyWidgetViewMode.Full;
    private DateTime playbackSyncedUtc = DateTime.UtcNow;
    private Bitmap? coverImage;
    private Bitmap? previousCoverImage;
    private Bitmap? nextCoverImage;
    private string coverImageUrl = "";
    private string previousCoverImageUrl = "";
    private string nextCoverImageUrl = "";
    private SpotifyTrackPreviewModel previousTrack = SpotifyTrackPreviewModel.Empty;
    private SpotifyTrackPreviewModel nextTrack = SpotifyTrackPreviewModel.Empty;
    private IReadOnlyList<SpotifyTrackPreviewModel> selectedPlaylistTracks = [];
    private SpotifyPlaylistViewModel? selectedPlaylist;
    private SpotifyDeviceViewModel? selectedDevice;
    private string statusText = "";
    private bool isBusy;
    private bool isLoadingPlaylists;
    private int tickCount;
    private int transportRefreshGeneration;
    private double carouselOpacity = 1;

    public SpotifyViewModel(SpotifyModel model, IWidgetLayoutProvider widgetLayoutProvider)
    {
        this.widgetLayoutProvider = widgetLayoutProvider;
        this.model = model.Normalize();
        statusText = this.model.IsConnected
            ? "Connected"
            : this.model.IsConfigured ? "Connect Spotify" : "Add your Spotify client ID in settings";
        timer.Subscribe(OnTimerTick);
        _ = RefreshAllAsync();
    }

    public ObservableCollection<SpotifyPlaylistViewModel> Playlists { get; } = [];
    public ObservableCollection<SpotifyDeviceViewModel> Devices { get; } = [];

    public bool IsConfigured => model.IsConfigured;
    public bool IsConnected => model.IsConnected;
    public bool IsBusy
    {
        get => isBusy;
        private set => this.RaiseAndSetIfChanged(ref isBusy, value);
    }

    public bool ShowConnect => IsConfigured && !IsConnected;
    public bool ShowSetup => !IsConfigured;
    public bool ShowPlayer => IsConnected;
    public bool ShowPlaylistPicker => IsConnected && viewMode == SpotifyWidgetViewMode.Full && Playlists.Count > 0;
    public bool ShowOpenSpotify => IsConnected && Devices.Count == 0;
    public bool HasCoverImage => CoverImage != null;
    public bool HasPreviousCoverImage => PreviousCoverImage != null;
    public bool HasNextCoverImage => NextCoverImage != null;
    public Bitmap? CoverImage
    {
        get => coverImage;
        private set
        {
            this.RaiseAndSetIfChanged(ref coverImage, value);
            this.RaisePropertyChanged(nameof(HasCoverImage));
        }
    }

    public Bitmap? PreviousCoverImage
    {
        get => previousCoverImage;
        private set
        {
            this.RaiseAndSetIfChanged(ref previousCoverImage, value);
            this.RaisePropertyChanged(nameof(HasPreviousCoverImage));
        }
    }

    public Bitmap? NextCoverImage
    {
        get => nextCoverImage;
        private set
        {
            this.RaiseAndSetIfChanged(ref nextCoverImage, value);
            this.RaisePropertyChanged(nameof(HasNextCoverImage));
        }
    }

    public SpotifyPlaylistViewModel? SelectedPlaylist
    {
        get => selectedPlaylist;
        set
        {
            this.RaiseAndSetIfChanged(ref selectedPlaylist, value);
            if (!isLoadingPlaylists && value != null && value.Id != model.SelectedPlaylistId)
                _ = PlayPlaylistAsync(value);
        }
    }

    public SpotifyDeviceViewModel? SelectedDevice
    {
        get => selectedDevice;
        set
        {
            this.RaiseAndSetIfChanged(ref selectedDevice, value);
            if (value != null && value.Id != model.SelectedDeviceId)
            {
                SaveModel(model with
                {
                    SelectedDeviceId = value.Id,
                    SelectedDeviceName = value.Name
                });
            }
        }
    }

    public double CarouselOpacity
    {
        get => carouselOpacity;
        private set => this.RaiseAndSetIfChanged(ref carouselOpacity, value);
    }

    public string StatusText
    {
        get => statusText;
        private set => this.RaiseAndSetIfChanged(ref statusText, value);
    }

    public string TrackTitle => playback.TrackTitle;
    public string ArtistText => playback.ArtistName;
    public string AlbumText => playback.AlbumName;
    public string PreviousTitle => string.IsNullOrWhiteSpace(previousTrack.Title) ? "Previous" : previousTrack.Title;
    public string NextTitle => string.IsNullOrWhiteSpace(nextTrack.Title) ? "Next" : nextTrack.Title;
    public string DeviceText => !string.IsNullOrWhiteSpace(playback.DeviceName)
        ? playback.DeviceName
        : SelectedDevice?.DisplayName ?? "Open Spotify on a device";
    public bool IsTrackPlaying => playback.IsPlaying;
    public bool IsTrackPaused => !playback.IsPlaying;
    public bool IsShuffleOn => playback.ShuffleOn;
    public double ShuffleOpacity => playback.ShuffleOn ? 1.0 : 0.45;
    public string ProgressText => playback.DurationMs <= 0 ? "--:--" : $"{FormatTime(CurrentProgressMs)} / {FormatTime(playback.DurationMs)}";
    public double ProgressValue => playback.DurationMs <= 0 ? 0 : Math.Clamp(CurrentProgressMs * 100.0 / playback.DurationMs, 0, 100);
    public IBrush MutedBrush => SolidColorBrush.Parse("#8E9AA1");
    public IBrush TextBrush => SolidColorBrush.Parse("#AEB8BE");

    public async Task ConnectAsync()
    {
        if (!model.IsConfigured || IsBusy)
            return;

        await RunAsync(async () =>
        {
            StatusText = "Waiting for Spotify login...";
            var token = await SpotifyOAuthService.AuthorizeAsync(model.ClientId, model.RedirectUri);
            SaveModel(model with
            {
                AccessToken = token.AccessToken,
                RefreshToken = token.RefreshToken,
                ExpiresAtUtc = token.ExpiresAtUtc
            });
            StatusText = "Connected";
            await LoadPlaylistsAndPlaybackAsync();
        });
    }

    public void Disconnect()
    {
        SaveModel(model with
        {
            AccessToken = "",
            RefreshToken = "",
            ExpiresAtUtc = null,
            SelectedPlaylistId = "",
            SelectedPlaylistName = "",
            SelectedDeviceId = "",
            SelectedDeviceName = ""
        });
        Playlists.Clear();
        Devices.Clear();
        SelectedPlaylist = null;
        selectedDevice = null;
        this.RaisePropertyChanged(nameof(SelectedDevice));
        playback = SpotifyPlaybackModel.Empty;
        previousTrack = SpotifyTrackPreviewModel.Empty;
        nextTrack = SpotifyTrackPreviewModel.Empty;
        CoverImage = null;
        PreviousCoverImage = null;
        NextCoverImage = null;
        coverImageUrl = "";
        previousCoverImageUrl = "";
        nextCoverImageUrl = "";
        StatusText = model.IsConfigured ? "Connect Spotify" : "Add your Spotify client ID in settings";
        RaiseAll();
    }

    public Task RefreshAllAsync() => RunAsync(async () =>
    {
        if (!model.IsConnected)
        {
            RaiseAll();
            return;
        }

        var token = await GetAccessTokenAsync();
        await LoadPlaylistsAndPlaybackAsync(token);
    });

    public Task RefreshPlaybackAsync() => RunAsync(async () =>
    {
        if (!model.IsConnected)
            return;

        await RefreshPlaybackAsync(await GetAccessTokenAsync());
    });

    public Task TogglePlayPauseAsync() => RunAsync(async () =>
    {
        var token = await GetAccessTokenAsync();
        if (playback.IsPlaying)
            await SpotifyWebApi.PauseAsync(token, await GetCommandDeviceIdAsync(token));
        else
            await SpotifyWebApi.ResumeAsync(token, await EnsurePlaybackDeviceAsync(token));

        await RefreshPlaybackAsync(token);
    });

    public Task ToggleShuffleAsync() => RunAsync(async () =>
    {
        var token = await GetAccessTokenAsync();
        await SpotifyWebApi.SetShuffleAsync(token, !playback.ShuffleOn, await GetCommandDeviceIdAsync(token));
        await Task.Delay(250);
        await RefreshPlaybackAsync(token);
    });

    public void OpenSpotify()
    {
        try
        {
            Process.Start(new ProcessStartInfo("spotify:") { UseShellExecute = true });
            StatusText = "Opening Spotify...";
        }
        catch (Exception ex)
        {
            StatusText = ex.Message;
        }
    }

    public Task NextAsync() => RunAsync(async () =>
    {
        var token = await GetAccessTokenAsync();
        var previousPlaybackId = playback.Id;
        var generation = ++transportRefreshGeneration;
        await SpotifyWebApi.NextAsync(token, await GetCommandDeviceIdAsync(token));
        var optimisticTrackId = OptimisticallyShowNextTrack();
        _ = RefreshPlaybackAfterTransportAsync(token, previousPlaybackId, optimisticTrackId, generation);
    });

    public Task PreviousAsync() => RunAsync(async () =>
    {
        var token = await GetAccessTokenAsync();
        var previousPlaybackId = playback.Id;
        var generation = ++transportRefreshGeneration;
        await SpotifyWebApi.PreviousAsync(token, await GetCommandDeviceIdAsync(token));
        var optimisticTrackId = OptimisticallyShowPreviousTrack();
        _ = RefreshPlaybackAfterTransportAsync(token, previousPlaybackId, optimisticTrackId, generation);
    });

    public Task PlaySelectedPlaylistAsync() =>
        SelectedPlaylist == null ? Task.CompletedTask : PlayPlaylistAsync(SelectedPlaylist);

    public Task PlayPlaylistAsync(SpotifyPlaylistViewModel playlist) => RunAsync(async () =>
    {
        var token = await GetAccessTokenAsync();
        var deviceId = await EnsurePlaybackDeviceAsync(token);
        await SpotifyWebApi.PlayPlaylistAsync(token, playlist.Id, deviceId);
        SaveModel(model with
        {
            SelectedPlaylistId = playlist.Id,
            SelectedPlaylistName = playlist.Name
        });
        await RefreshSelectedPlaylistTracksAsync(token);
        await Task.Delay(500);
        await RefreshPlaybackAsync(token);
    });

    public void SetViewport(double width, double height)
    {
        var nextMode = width < 310 || height < 230
            ? SpotifyWidgetViewMode.Compact
            : SpotifyWidgetViewMode.Full;

        if (nextMode == viewMode)
            return;

        viewMode = nextMode;
        RaiseAll();
    }

    public void ApplyModel(SpotifyModel nextModel)
    {
        model = nextModel.Normalize();
        StatusText = model.IsConnected
            ? "Connected"
            : model.IsConfigured ? "Connect Spotify" : "Add your Spotify client ID in settings";
        RaiseAll();
        _ = RefreshAllAsync();
    }

    public void Dispose()
    {
        timer.Unsubscribe(OnTimerTick);
        CoverImage?.Dispose();
        PreviousCoverImage?.Dispose();
        NextCoverImage?.Dispose();
        GC.SuppressFinalize(this);
    }

    private async Task LoadPlaylistsAndPlaybackAsync(string? existingToken = null)
    {
        var token = existingToken ?? await GetAccessTokenAsync();
        await LoadDevicesAsync(token);
        isLoadingPlaylists = true;
        try
        {
            var loadedPlaylists = await SpotifyWebApi.GetPlaylistsAsync(token);
            Playlists.Clear();
            foreach (var playlist in loadedPlaylists)
                Playlists.Add(new SpotifyPlaylistViewModel(playlist));

            SelectedPlaylist = Playlists.FirstOrDefault(playlist => playlist.Id == model.SelectedPlaylistId)
                ?? Playlists.FirstOrDefault();
        }
        finally
        {
            isLoadingPlaylists = false;
        }

        await RefreshSelectedPlaylistTracksAsync(token);
        await RefreshPlaybackAsync(token);
    }

    private async Task LoadDevicesAsync(string token)
    {
        var loadedDevices = await SpotifyWebApi.GetDevicesAsync(token);
        Devices.Clear();
        foreach (var device in loadedDevices)
            Devices.Add(new SpotifyDeviceViewModel(device));

        var nextDevice = Devices.FirstOrDefault(device => device.Id == model.SelectedDeviceId)
            ?? Devices.FirstOrDefault(device => device.IsActive)
            ?? Devices.FirstOrDefault();

        selectedDevice = nextDevice;
        this.RaisePropertyChanged(nameof(SelectedDevice));

        if (nextDevice != null && nextDevice.Id != model.SelectedDeviceId)
        {
            SaveModel(model with
            {
                SelectedDeviceId = nextDevice.Id,
                SelectedDeviceName = nextDevice.Name
            });
        }

        this.RaisePropertyChanged(nameof(ShowOpenSpotify));
    }

    private async Task<string> EnsurePlaybackDeviceAsync(string token)
    {
        var deviceId = await GetCommandDeviceIdAsync(token);
        if (SelectedDevice is { IsActive: false })
        {
            StatusText = $"Activating {SelectedDevice.Name}...";
            await SpotifyWebApi.TransferPlaybackAsync(token, deviceId);
            await Task.Delay(350);
        }

        return deviceId;
    }

    private async Task<string> GetCommandDeviceIdAsync(string token)
    {
        if (SelectedDevice != null)
            return SelectedDevice.Id;

        await LoadDevicesAsync(token);
        if (SelectedDevice != null)
            return SelectedDevice.Id;

        throw new InvalidOperationException("No active device found. Open Spotify on your computer or phone, start any song once, then refresh this widget.");
    }

    private async Task RefreshPlaybackAsync(string token)
    {
        var nextPlayback = await SpotifyWebApi.GetPlaybackAsync(token);
        await ApplyPlaybackAsync(nextPlayback, token);
    }

    private async Task RefreshPlaybackAfterTransportAsync(
        string token,
        string previousPlaybackId,
        string optimisticTrackId,
        int generation)
    {
        try
        {
            for (var attempt = 0; attempt < 4; attempt++)
            {
                await Task.Delay(attempt == 0 ? 220 : 320);
                if (generation != transportRefreshGeneration)
                    return;

                var nextPlayback = await SpotifyWebApi.GetPlaybackAsync(token);
                if (generation != transportRefreshGeneration)
                    return;

                if (string.IsNullOrWhiteSpace(optimisticTrackId) ||
                    string.IsNullOrWhiteSpace(nextPlayback.Id) ||
                    nextPlayback.Id == optimisticTrackId ||
                    nextPlayback.Id != previousPlaybackId)
                {
                    await ApplyPlaybackAsync(nextPlayback, token);
                    return;
                }
            }

            await RefreshQueuePreviewAsync(token);
            RaiseAll();
        }
        catch (Exception ex)
        {
            if (generation == transportRefreshGeneration)
            {
                StatusText = ex.Message;
                RaiseAll();
            }
        }
    }

    private async Task ApplyPlaybackAsync(SpotifyPlaybackModel nextPlayback, string token)
    {
        if (!string.IsNullOrWhiteSpace(playback.Id) &&
            !string.IsNullOrWhiteSpace(nextPlayback.Id) &&
            playback.Id != nextPlayback.Id)
        {
            previousTrack = new SpotifyTrackPreviewModel(
                playback.Id,
                playback.TrackTitle,
                playback.ArtistName,
                playback.ImageUrl);
            await UpdatePreviousCoverAsync(previousTrack.ImageUrl);
            _ = PulseCarouselAsync();
        }

        playback = nextPlayback;
        playbackSyncedUtc = DateTime.UtcNow;
        await UpdateCoverAsync(playback.ImageUrl);
        await RefreshQueuePreviewAsync(token);
        StatusText = playback.TrackTitle == SpotifyPlaybackModel.Empty.TrackTitle
            ? "Open Spotify on any device"
            : "Connected";
        RaisePlayback();
    }

    private string OptimisticallyShowNextTrack()
    {
        var promotedTrack = !string.IsNullOrWhiteSpace(nextTrack.Id)
            ? nextTrack
            : GetPlaylistNeighbor(playback.Id, 1);
        if (string.IsNullOrWhiteSpace(promotedTrack.Id))
            return "";

        var promotedCover = NextCoverImage;
        var promotedCoverUrl = nextCoverImageUrl;
        var oldPreviousCover = PreviousCoverImage;

        previousTrack = CurrentTrackPreview();
        previousCoverImageUrl = coverImageUrl;
        PreviousCoverImage = CoverImage;

        playback = playback with
        {
            Id = promotedTrack.Id,
            Uri = "",
            TrackTitle = promotedTrack.Title,
            ArtistName = promotedTrack.ArtistName,
            AlbumName = "",
            ImageUrl = promotedTrack.ImageUrl,
            ProgressMs = 0
        };
        playbackSyncedUtc = DateTime.UtcNow;

        CoverImage = promotedCover;
        coverImageUrl = promotedCoverUrl;
        nextTrack = SpotifyTrackPreviewModel.Empty;
        nextCoverImageUrl = "";
        NextCoverImage = null;

        DisposeIfUnreferenced(oldPreviousCover);
        _ = LoadPlaylistNeighborPreviewAsync(promotedTrack.Id, 1);
        _ = PulseCarouselAsync();
        RaiseAll();
        return promotedTrack.Id;
    }

    private string OptimisticallyShowPreviousTrack()
    {
        var promotedTrack = !string.IsNullOrWhiteSpace(previousTrack.Id)
            ? previousTrack
            : GetPlaylistNeighbor(playback.Id, -1);
        if (string.IsNullOrWhiteSpace(promotedTrack.Id))
            return "";

        var promotedCover = PreviousCoverImage;
        var promotedCoverUrl = previousCoverImageUrl;
        var oldNextCover = NextCoverImage;

        nextTrack = CurrentTrackPreview();
        nextCoverImageUrl = coverImageUrl;
        NextCoverImage = CoverImage;

        playback = playback with
        {
            Id = promotedTrack.Id,
            Uri = "",
            TrackTitle = promotedTrack.Title,
            ArtistName = promotedTrack.ArtistName,
            AlbumName = "",
            ImageUrl = promotedTrack.ImageUrl,
            ProgressMs = 0
        };
        playbackSyncedUtc = DateTime.UtcNow;

        CoverImage = promotedCover;
        coverImageUrl = promotedCoverUrl;
        previousTrack = SpotifyTrackPreviewModel.Empty;
        previousCoverImageUrl = "";
        PreviousCoverImage = null;

        DisposeIfUnreferenced(oldNextCover);
        _ = LoadPlaylistNeighborPreviewAsync(promotedTrack.Id, -1);
        _ = PulseCarouselAsync();
        RaiseAll();
        return promotedTrack.Id;
    }

    private SpotifyTrackPreviewModel CurrentTrackPreview() => new(
        playback.Id,
        playback.TrackTitle,
        playback.ArtistName,
        playback.ImageUrl);

    private void DisposeIfUnreferenced(Bitmap? bitmap)
    {
        if (bitmap == null ||
            ReferenceEquals(bitmap, CoverImage) ||
            ReferenceEquals(bitmap, PreviousCoverImage) ||
            ReferenceEquals(bitmap, NextCoverImage))
            return;

        bitmap.Dispose();
    }

    private SpotifyTrackPreviewModel GetPlaylistNeighbor(string trackId, int offset)
    {
        if (string.IsNullOrWhiteSpace(trackId) || selectedPlaylistTracks.Count == 0)
            return SpotifyTrackPreviewModel.Empty;

        for (var index = 0; index < selectedPlaylistTracks.Count; index++)
        {
            if (selectedPlaylistTracks[index].Id != trackId)
                continue;

            var nextIndex = (index + offset) % selectedPlaylistTracks.Count;
            if (nextIndex < 0)
                nextIndex += selectedPlaylistTracks.Count;

            return selectedPlaylistTracks[nextIndex];
        }

        return SpotifyTrackPreviewModel.Empty;
    }

    private async Task LoadPlaylistNeighborPreviewAsync(string trackId, int offset)
    {
        try
        {
            var neighbor = GetPlaylistNeighbor(trackId, offset);
            if (string.IsNullOrWhiteSpace(neighbor.Id))
                return;

            if (offset > 0)
            {
                nextTrack = neighbor;
                await UpdateNextCoverAsync(neighbor.ImageUrl);
            }
            else
            {
                previousTrack = neighbor;
                await UpdatePreviousCoverAsync(neighbor.ImageUrl);
            }

            RaiseAll();
        }
        catch
        {
        }
    }

    private async Task<string> GetAccessTokenAsync()
    {
        if (!model.IsConnected)
            throw new InvalidOperationException("Connect Spotify first.");

        if (!string.IsNullOrWhiteSpace(model.AccessToken) &&
            model.ExpiresAtUtc > DateTime.UtcNow.AddMinutes(1))
            return model.AccessToken;

        var token = await SpotifyOAuthService.RefreshAsync(model.ClientId, model.RefreshToken);
        SaveModel(model with
        {
            AccessToken = token.AccessToken,
            RefreshToken = token.RefreshToken,
            ExpiresAtUtc = token.ExpiresAtUtc
        });

        return token.AccessToken;
    }

    private void SaveModel(SpotifyModel newModel)
    {
        model = newModel.Normalize();
        var layout = widgetLayoutProvider.Get();
        widgetLayoutProvider.Save(layout with { Settings = JsonSerializer.SerializeToElement(model) });
        RaiseAll();
    }

    private async Task UpdateCoverAsync(string imageUrl)
    {
        if (imageUrl == coverImageUrl)
            return;

        coverImageUrl = imageUrl;
        if (string.IsNullOrWhiteSpace(imageUrl))
        {
            CoverImage = null;
            return;
        }

        try
        {
            await using var stream = await ImageHttp.GetStreamAsync(imageUrl);
            using var memory = new MemoryStream();
            await stream.CopyToAsync(memory);
            memory.Position = 0;
            var previous = CoverImage;
            CoverImage = new Bitmap(memory);
            previous?.Dispose();
        }
        catch
        {
            CoverImage = null;
        }
    }

    private async Task RefreshQueuePreviewAsync(string token)
    {
        try
        {
            nextTrack = await SpotifyWebApi.GetNextTrackAsync(token);
            if (string.IsNullOrWhiteSpace(nextTrack.Id) || nextTrack.Id == playback.Id)
                nextTrack = GetPlaylistNeighbor(playback.Id, 1);

            await UpdateNextCoverAsync(nextTrack.ImageUrl);
        }
        catch
        {
            var fallback = GetPlaylistNeighbor(playback.Id, 1);
            if (!string.IsNullOrWhiteSpace(fallback.Id))
            {
                nextTrack = fallback;
                await UpdateNextCoverAsync(fallback.ImageUrl);
            }
        }
    }

    private async Task RefreshSelectedPlaylistTracksAsync(string token)
    {
        if (SelectedPlaylist == null)
        {
            selectedPlaylistTracks = [];
            return;
        }

        try
        {
            selectedPlaylistTracks = await SpotifyWebApi.GetPlaylistTracksAsync(token, SelectedPlaylist.Id);
        }
        catch
        {
            selectedPlaylistTracks = [];
        }
    }

    private async Task UpdatePreviousCoverAsync(string imageUrl)
    {
        if (imageUrl == previousCoverImageUrl)
            return;

        previousCoverImageUrl = imageUrl;
        var previous = PreviousCoverImage;
        PreviousCoverImage = await LoadImageAsync(imageUrl);
        previous?.Dispose();
    }

    private async Task UpdateNextCoverAsync(string imageUrl)
    {
        if (imageUrl == nextCoverImageUrl)
            return;

        nextCoverImageUrl = imageUrl;
        var previous = NextCoverImage;
        NextCoverImage = await LoadImageAsync(imageUrl);
        previous?.Dispose();
    }

    private async Task<Bitmap?> LoadImageAsync(string imageUrl)
    {
        if (string.IsNullOrWhiteSpace(imageUrl))
            return null;

        try
        {
            await using var stream = await ImageHttp.GetStreamAsync(imageUrl);
            using var memory = new MemoryStream();
            await stream.CopyToAsync(memory);
            memory.Position = 0;
            return new Bitmap(memory);
        }
        catch
        {
            return null;
        }
    }

    private async Task RunAsync(Func<Task> action)
    {
        if (IsBusy)
            return;

        IsBusy = true;
        try
        {
            await action();
        }
        catch (Exception ex)
        {
            StatusText = ex.Message;
        }
        finally
        {
            IsBusy = false;
            RaiseAll();
        }
    }

    private void OnTimerTick()
    {
        RaisePlayback();
        tickCount++;
        if (tickCount % 6 == 0 && model.IsConnected && !IsBusy)
            _ = RefreshPlaybackAsync();
    }

    private int CurrentProgressMs
    {
        get
        {
            if (!playback.IsPlaying || playback.DurationMs <= 0)
                return playback.ProgressMs;

            var elapsed = (int)(DateTime.UtcNow - playbackSyncedUtc).TotalMilliseconds;
            return Math.Clamp(playback.ProgressMs + elapsed, 0, playback.DurationMs);
        }
    }

    private static string FormatTime(int milliseconds)
    {
        var time = TimeSpan.FromMilliseconds(Math.Max(0, milliseconds));
        return $"{(int)time.TotalMinutes}:{time.Seconds:00}";
    }

    private void RaiseAll()
    {
        this.RaisePropertyChanged(nameof(IsConfigured));
        this.RaisePropertyChanged(nameof(IsConnected));
        this.RaisePropertyChanged(nameof(ShowConnect));
        this.RaisePropertyChanged(nameof(ShowSetup));
        this.RaisePropertyChanged(nameof(ShowPlayer));
        this.RaisePropertyChanged(nameof(ShowPlaylistPicker));
        this.RaisePropertyChanged(nameof(ShowOpenSpotify));
        this.RaisePropertyChanged(nameof(HasCoverImage));
        this.RaisePropertyChanged(nameof(HasPreviousCoverImage));
        this.RaisePropertyChanged(nameof(HasNextCoverImage));
        RaisePlayback();
    }

    private void RaisePlayback()
    {
        this.RaisePropertyChanged(nameof(TrackTitle));
        this.RaisePropertyChanged(nameof(ArtistText));
        this.RaisePropertyChanged(nameof(AlbumText));
        this.RaisePropertyChanged(nameof(PreviousTitle));
        this.RaisePropertyChanged(nameof(NextTitle));
        this.RaisePropertyChanged(nameof(DeviceText));
        this.RaisePropertyChanged(nameof(IsTrackPlaying));
        this.RaisePropertyChanged(nameof(IsTrackPaused));
        this.RaisePropertyChanged(nameof(IsShuffleOn));
        this.RaisePropertyChanged(nameof(ShuffleOpacity));
        this.RaisePropertyChanged(nameof(ProgressText));
        this.RaisePropertyChanged(nameof(ProgressValue));
        this.RaisePropertyChanged(nameof(TextBrush));
        this.RaisePropertyChanged(nameof(MutedBrush));
    }

    private async Task PulseCarouselAsync()
    {
        CarouselOpacity = 0.35;
        await Task.Delay(120);
        CarouselOpacity = 1;
    }
}
