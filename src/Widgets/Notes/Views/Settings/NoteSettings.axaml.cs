using Avalonia.Controls;
using Avalonia.Interactivity;
using Notes.ViewModels;
using uWidgets.Core.Interfaces;

namespace Notes.Views.Settings;

public partial class NoteSettings : UserControl
{
    private readonly NoteSettingsViewModel viewModel;
    private readonly IWidgetFactory<Window, UserControl> widgetFactory;

    public NoteSettings(
        IWidgetLayoutProvider widgetLayoutProvider,
        IWidgetFactory<Window, UserControl> widgetFactory)
    {
        this.widgetFactory = widgetFactory;
        viewModel = new NoteSettingsViewModel(widgetLayoutProvider);
        DataContext = viewModel;
        InitializeComponent();
    }

    private void Clear_OnClick(object? sender, RoutedEventArgs e) => viewModel.Clear();

    private void DuplicateStyle_OnClick(object? sender, RoutedEventArgs e) =>
        widgetFactory.Add(viewModel.CreateDuplicate(false)).Show();

    private void DuplicateContent_OnClick(object? sender, RoutedEventArgs e) =>
        widgetFactory.Add(viewModel.CreateDuplicate(true)).Show();
}
