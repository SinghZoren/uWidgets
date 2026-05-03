using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Pomodoro.ViewModels;
using uWidgets.Core.Interfaces;

namespace Pomodoro.Views.Settings;

public partial class PomodoroSettings : UserControl
{
    private readonly PomodoroSettingsViewModel viewModel;

    public PomodoroSettings(IWidgetLayoutProvider widgetLayoutProvider)
    {
        viewModel = new PomodoroSettingsViewModel(widgetLayoutProvider);
        DataContext = viewModel;
        InitializeComponent();
    }

    private async void ChooseAlarmSound_OnClick(object? sender, RoutedEventArgs e)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel?.StorageProvider == null)
            return;

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Choose Pomodoro alarm MP3",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("MP3 audio")
                {
                    Patterns = ["*.mp3"],
                    MimeTypes = ["audio/mpeg"]
                }
            ]
        });

        var path = files.FirstOrDefault()?.TryGetLocalPath();
        if (!string.IsNullOrWhiteSpace(path))
            viewModel.AlarmSoundPath = path;
    }

    private void TestAlarmSound_OnClick(object? sender, RoutedEventArgs e) => viewModel.TestAlarmSound();

    private void ClearAlarmSound_OnClick(object? sender, RoutedEventArgs e) => viewModel.ClearAlarmSound();
}
