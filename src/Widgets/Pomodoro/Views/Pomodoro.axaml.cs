using Avalonia.Controls;
using Avalonia;
using Avalonia.Interactivity;
using Pomodoro.Locales;
using Pomodoro.Models;
using Pomodoro.ViewModels;
using uWidgets.Core.Interfaces;

namespace Pomodoro.Views;

public partial class Pomodoro : UserControl
{
    public Pomodoro(IWidgetLayoutProvider widgetLayoutProvider)
        : this(new PomodoroModel(), widgetLayoutProvider)
    {
    }

    public Pomodoro(PomodoroModel model, IWidgetLayoutProvider widgetLayoutProvider)
    {
        var viewModel = new PomodoroViewModel(model, widgetLayoutProvider);
        DataContext = viewModel;
        InitializeComponent();

        SizeChanged += OnSizeChanged;
        Unloaded += OnUnloaded;
    }

    private PomodoroViewModel ViewModel => (PomodoroViewModel)DataContext!;

    private void StartPause_OnClick(object? sender, RoutedEventArgs e) => ViewModel.StartPause();

    private void Reset_OnClick(object? sender, RoutedEventArgs e) => ViewModel.Reset();

    private void Skip_OnClick(object? sender, RoutedEventArgs e) => ViewModel.Skip();

    private void OnSizeChanged(object? sender, SizeChangedEventArgs e) =>
        ViewModel.SetViewport(e.NewSize.Width, e.NewSize.Height);

    private void OnUnloaded(object? sender, RoutedEventArgs e)
    {
        SizeChanged -= OnSizeChanged;
        Unloaded -= OnUnloaded;
        ViewModel.Dispose();
    }
}
