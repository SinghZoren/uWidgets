using Avalonia.Controls;
using Avalonia.Interactivity;
using Spotify.ViewModels;
using uWidgets.Core.Interfaces;

namespace Spotify.Views.Settings;

public partial class SpotifySettings : UserControl
{
    private readonly SpotifySettingsViewModel viewModel;

    public SpotifySettings(IWidgetLayoutProvider widgetLayoutProvider)
    {
        viewModel = new SpotifySettingsViewModel(widgetLayoutProvider);
        DataContext = viewModel;
        InitializeComponent();
    }

    private async void Connect_OnClick(object? sender, RoutedEventArgs e) => await viewModel.ConnectAsync();

    private void Disconnect_OnClick(object? sender, RoutedEventArgs e) => viewModel.Disconnect();
}
