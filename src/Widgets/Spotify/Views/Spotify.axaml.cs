using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Spotify.Models;
using Spotify.ViewModels;
using uWidgets.Core.Interfaces;
using uWidgets.Core.Models;

namespace Spotify.Views;

public partial class Spotify : UserControl, IWidgetSettingsUpdateHandler
{
    public Spotify(IWidgetLayoutProvider widgetLayoutProvider)
        : this(new SpotifyModel(), widgetLayoutProvider)
    {
    }

    public Spotify(SpotifyModel model, IWidgetLayoutProvider widgetLayoutProvider)
    {
        DataContext = new SpotifyViewModel(model, widgetLayoutProvider);
        InitializeComponent();
        SizeChanged += OnSizeChanged;
        Unloaded += OnUnloaded;
    }

    private SpotifyViewModel ViewModel => (SpotifyViewModel)DataContext!;

    public bool TryHandleSettingsUpdate(WidgetLayout oldLayout, WidgetLayout newLayout)
    {
        var nextModel = newLayout.GetModel<SpotifyModel>()?.Normalize();
        if (nextModel == null)
            return false;

        ViewModel.ApplyModel(nextModel);
        return true;
    }

    private async void Connect_OnClick(object? sender, RoutedEventArgs e) => await ViewModel.ConnectAsync();

    private async void Refresh_OnClick(object? sender, RoutedEventArgs e) => await ViewModel.RefreshAllAsync();

    private async void Previous_OnClick(object? sender, RoutedEventArgs e) => await ViewModel.PreviousAsync();

    private async void PlayPause_OnClick(object? sender, RoutedEventArgs e) => await ViewModel.TogglePlayPauseAsync();

    private async void Shuffle_OnClick(object? sender, RoutedEventArgs e) => await ViewModel.ToggleShuffleAsync();

    private async void Next_OnClick(object? sender, RoutedEventArgs e) => await ViewModel.NextAsync();

    private void OpenSpotify_OnClick(object? sender, RoutedEventArgs e) => ViewModel.OpenSpotify();

    private async void Playlist_OnClick(object? sender, RoutedEventArgs e)
    {
        if ((sender as Control)?.DataContext is SpotifyPlaylistViewModel playlist)
            await ViewModel.PlayPlaylistAsync(playlist);
    }

    private async void PlayPlaylist_OnClick(object? sender, RoutedEventArgs e) => await ViewModel.PlaySelectedPlaylistAsync();

    private void OnSizeChanged(object? sender, SizeChangedEventArgs e) =>
        ViewModel.SetViewport(e.NewSize.Width, e.NewSize.Height);

    private void OnUnloaded(object? sender, RoutedEventArgs e)
    {
        SizeChanged -= OnSizeChanged;
        Unloaded -= OnUnloaded;
        ViewModel.Dispose();
    }
}
