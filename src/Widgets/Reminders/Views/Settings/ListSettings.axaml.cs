using Avalonia.Controls;
using Avalonia.Interactivity;
using Reminders.ViewModels;
using uWidgets.Core.Interfaces;

namespace Reminders.Views.Settings;

public partial class ListSettings : UserControl
{
    private readonly RemindersSettingsViewModel viewModel;

    public ListSettings(IWidgetLayoutProvider widgetLayoutProvider)
    {
        viewModel = new RemindersSettingsViewModel(widgetLayoutProvider);
        DataContext = viewModel;
        InitializeComponent();
    }

    private void DeleteCompleted(object? sender, RoutedEventArgs e) => viewModel.DeleteCompleted();

    private void DeleteAll(object? sender, RoutedEventArgs e) => viewModel.DeleteAll();

    private void UndoDelete(object? sender, RoutedEventArgs e) => viewModel.UndoDelete();

    private void ExportJson(object? sender, RoutedEventArgs e) => viewModel.ExportJson();

    private void ImportJson(object? sender, RoutedEventArgs e) => viewModel.ImportLatestJson();
}
