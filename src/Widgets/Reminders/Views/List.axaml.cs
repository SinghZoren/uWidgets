using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Reminders.Locales;
using Reminders.Models;
using Reminders.Services;
using Reminders.ViewModels;
using uWidgets.Core;
using uWidgets.Core.Interfaces;
using uWidgets.Core.Models;

namespace Reminders.Views;

public partial class List : UserControl, IWidgetSettingsUpdateHandler
{
    private const string DragDataFormat = "uWidgets.todo.task";
    private RemindersListModel model;
    private readonly IWidgetLayoutProvider widgetLayoutProvider;
    private readonly TodoWidgetBridge todoBridge;
    private readonly RemindersViewModel viewModel;
    private readonly bool showOpenButton;
    private TodoAppWindow? todoAppWindow;
    private bool saving;

    public List(IWidgetLayoutProvider widgetLayoutProvider)
        : this(new RemindersListModel(Locale.Reminders_List_Title, []), widgetLayoutProvider) {}

    public List(RemindersListModel model, IWidgetLayoutProvider widgetLayoutProvider)
        : this(model, widgetLayoutProvider, true) {}

    internal List(RemindersListModel model, IWidgetLayoutProvider widgetLayoutProvider, bool showOpenButton)
    {
        this.widgetLayoutProvider = widgetLayoutProvider;
        this.model = model.Normalize();
        todoBridge = new TodoWidgetBridge(TodoStore.Shared, this.model);
        this.model = todoBridge.GetWidgetModel(this.model).Normalize();
        this.showOpenButton = showOpenButton;
        viewModel = new RemindersViewModel(this.model);
        DataContext = viewModel;

        InitializeComponent();
        OpenAppButton.IsVisible = showOpenButton;

        AddHandler(DragDrop.DragOverEvent, OnDragOver, RoutingStrategies.Bubble);
        AddHandler(DragDrop.DropEvent, OnDrop, RoutingStrategies.Bubble);
        AttachedToVisualTree += OnAttachedToVisualTree;
        Unloaded += OnUnloaded;
        widgetLayoutProvider.DataChanged += OnWidgetLayoutUpdated;
        todoBridge.DataChanged += OnTodoDataChanged;
        BackupTodos();
    }

    public bool TryHandleSettingsUpdate(WidgetLayout oldLayout, WidgetLayout newLayout)
    {
        var newModel = newLayout.GetModel<RemindersListModel>()?.Normalize();
        if (newModel == null) return false;
        if (saving) return true;

        ApplyModel(todoBridge.GetWidgetModel(newModel));
        return true;
    }

    private void OnAttachedToVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
    {
        ApplyWindowOptions();
    }

    private void OnUnloaded(object? sender, RoutedEventArgs e)
    {
        RemoveHandler(DragDrop.DragOverEvent, OnDragOver);
        RemoveHandler(DragDrop.DropEvent, OnDrop);
        AttachedToVisualTree -= OnAttachedToVisualTree;
        Unloaded -= OnUnloaded;
        widgetLayoutProvider.DataChanged -= OnWidgetLayoutUpdated;
        todoBridge.DataChanged -= OnTodoDataChanged;
    }

    private void OnWidgetLayoutUpdated(object sender, WidgetLayout? oldLayout, WidgetLayout newLayout)
    {
        void Update() => TryHandleSettingsUpdate(oldLayout ?? newLayout, newLayout);

        if (Dispatcher.UIThread.CheckAccess())
            Update();
        else
            Dispatcher.UIThread.Post(Update);
    }

    private void OnTodoDataChanged(object? sender, EventArgs e)
    {
        void Update() => ApplyModel(todoBridge.GetWidgetModel(model));

        if (Dispatcher.UIThread.CheckAccess())
            Update();
        else
            Dispatcher.UIThread.Post(Update);
    }

    public void ListNameChanged(object? sender, RoutedEventArgs e)
    {
        var listName = (sender as TextBox)?.Text;
        UpdateModel(model with { ListName = string.IsNullOrWhiteSpace(listName) ? Locale.Reminders_List_Title : listName });
    }

    private void QuickAdd_OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || sender is not TextBox textBox)
            return;

        var text = textBox.Text?.Trim();
        if (string.IsNullOrWhiteSpace(text))
            return;

        if (e.KeyModifiers.HasFlag(KeyModifiers.Control) && !string.IsNullOrWhiteSpace(model.SelectedTaskId))
        {
            AddSubtask(model.SelectedTaskId!, text);
        }
        else
        {
            AddTask(text);
        }

        textBox.Clear();
        e.Handled = true;
    }

    private void Root_OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Source is TextBox or ComboBox)
            return;

        if (string.IsNullOrWhiteSpace(model.SelectedTaskId))
            return;

        if (e.Key == Key.Delete)
        {
            DeleteTask(model.SelectedTaskId!);
            e.Handled = true;
        }
        else if (e.Key == Key.Space)
        {
            ToggleTask(model.SelectedTaskId!);
            e.Handled = true;
        }
    }

    private void Root_OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!model.LockLayout || IsInteractive(e.Source as Control))
            return;

        e.Handled = true;
    }

    private void TaskRow_OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if ((sender as Control)?.DataContext is TodoTaskViewModel task)
            SelectTask(task.Id);

        if (IsInteractive(e.Source as Control))
            return;

        e.Handled = true;
    }

    private void TaskComplete_OnClick(object? sender, RoutedEventArgs e)
    {
        if ((sender as Control)?.DataContext is TodoTaskViewModel task)
            ToggleTask(task.Id);
    }

    private void TaskTitle_OnLostFocus(object? sender, RoutedEventArgs e)
    {
        if ((sender as TextBox)?.DataContext is not TodoTaskViewModel task)
            return;

        var text = (sender as TextBox)?.Text?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(text))
            DeleteTask(task.Id);
        else
            UpdateTask(task.Id, reminder => reminder with { Title = text });
    }

    private void TaskDueDate_OnLostFocus(object? sender, RoutedEventArgs e)
    {
        if ((sender as TextBox)?.DataContext is not TodoTaskViewModel task)
            return;

        var text = (sender as TextBox)?.Text?.Trim();
        if (string.IsNullOrWhiteSpace(text))
        {
            UpdateTask(task.Id, reminder => reminder with { DueDate = null });
            return;
        }

        if (DateTime.TryParse(text, out var dueDate))
            UpdateTask(task.Id, reminder => reminder with { DueDate = dueDate.Date });
    }

    private void TaskPriority_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if ((sender as ComboBox)?.DataContext is TodoTaskViewModel task &&
            (sender as ComboBox)?.SelectedItem is TodoPriority priority)
            UpdateTask(task.Id, reminder => reminder with { Priority = priority });
    }

    private void TaskCategory_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if ((sender as ComboBox)?.DataContext is TodoTaskViewModel task &&
            (sender as ComboBox)?.SelectedItem is string category)
            UpdateTask(task.Id, reminder => reminder with { Category = category });
    }

    private void TaskRecurrence_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if ((sender as ComboBox)?.DataContext is TodoTaskViewModel task &&
            (sender as ComboBox)?.SelectedItem is TodoRecurrence recurrence)
            UpdateTask(task.Id, reminder => reminder with { Recurrence = recurrence });
    }

    private void TaskSection_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if ((sender as ComboBox)?.DataContext is TodoTaskViewModel task &&
            (sender as ComboBox)?.SelectedItem is TodoSection section)
        {
            if (task.Section == section)
                return;

            MoveTaskToSection(task.Id, section);
        }
    }

    private void TaskNotes_OnLostFocus(object? sender, RoutedEventArgs e)
    {
        if ((sender as TextBox)?.DataContext is TodoTaskViewModel task)
            UpdateTask(task.Id, reminder => reminder with { Notes = (sender as TextBox)?.Text ?? "" });
    }

    private void DeleteTask_OnClick(object? sender, RoutedEventArgs e)
    {
        if ((sender as Control)?.DataContext is TodoTaskViewModel task)
            DeleteTask(task.Id);
    }

    private void MoveTaskUp_OnClick(object? sender, RoutedEventArgs e)
    {
        if ((sender as Control)?.DataContext is TodoTaskViewModel task)
            MoveTaskBy(task.Id, -1);
    }

    private void MoveTaskDown_OnClick(object? sender, RoutedEventArgs e)
    {
        if ((sender as Control)?.DataContext is TodoTaskViewModel task)
            MoveTaskBy(task.Id, 1);
    }

    private async void DragHandle_OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if ((sender as Control)?.DataContext is not TodoTaskViewModel task)
            return;

        var data = new DataObject();
        data.Set(DragDataFormat, task.Id);
        await DragDrop.DoDragDrop(e, data, DragDropEffects.Move);
    }

    private void OnDragOver(object? sender, DragEventArgs e)
    {
        if (!e.Data.Contains(DragDataFormat))
            return;

        e.DragEffects = DragDropEffects.Move;
        e.Handled = true;
    }

    private void OnDrop(object? sender, DragEventArgs e)
    {
        var sourceId = e.Data.Get(DragDataFormat) as string;
        if (string.IsNullOrWhiteSpace(sourceId))
            return;

        var control = e.Source as Control;
        while (control != null)
        {
            if (control.DataContext is TodoTaskViewModel target)
            {
                MoveTaskBefore(sourceId, target.Id, target.Section);
                e.Handled = true;
                return;
            }

            if (control.DataContext is TodoSectionViewModel section)
            {
                MoveTaskToSection(sourceId, section.Section);
                e.Handled = true;
                return;
            }

            control = control.Parent as Control;
        }

        e.Handled = true;
    }

    private void SubtaskComplete_OnClick(object? sender, RoutedEventArgs e)
    {
        if ((sender as Control)?.DataContext is TodoSubtaskViewModel subtask)
            UpdateSubtask(subtask.TaskId, subtask.Id, item => item with { Completed = !item.Completed });
    }

    private void SubtaskTitle_OnLostFocus(object? sender, RoutedEventArgs e)
    {
        if ((sender as TextBox)?.DataContext is not TodoSubtaskViewModel subtask)
            return;

        var text = (sender as TextBox)?.Text?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(text))
            RemoveSubtask(subtask.TaskId, subtask.Id);
        else
            UpdateSubtask(subtask.TaskId, subtask.Id, item => item with { Title = text });
    }

    private void AddSubtask_OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || (sender as TextBox)?.DataContext is not TodoTaskViewModel task)
            return;

        var textBox = (TextBox)sender!;
        var text = textBox.Text?.Trim();
        if (string.IsNullOrWhiteSpace(text))
            return;

        AddSubtask(task.Id, text);
        textBox.Clear();
        e.Handled = true;
    }

    private void UndoDelete_OnClick(object? sender, RoutedEventArgs e) => UndoDelete();

    private void OpenTodoApp_OnClick(object? sender, RoutedEventArgs e)
    {
        if (!showOpenButton)
            return;

        if (todoAppWindow != null)
        {
            todoAppWindow.Activate();
            return;
        }

        todoAppWindow = new TodoAppWindow(model, widgetLayoutProvider);
        todoAppWindow.Closed += (_, _) => todoAppWindow = null;
        todoAppWindow.Show();
    }

    public void CompleteReminder(object? sender, RoutedEventArgs e)
    {
        if ((sender as Control)?.DataContext is ReminderModel reminder)
            ToggleTask(reminder.Id ?? "");
    }

    public void EditReminder(object? sender, RoutedEventArgs e)
    {
        if ((sender as TextBox)?.DataContext is not ReminderModel reminder)
            return;

        var text = (sender as TextBox)?.Text?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(text))
            DeleteTask(reminder.Id ?? "");
        else
            UpdateTask(reminder.Id ?? "", task => task with { Title = text });
    }

    public void CreateReminder(object? sender, RoutedEventArgs e)
    {
        var textBox = sender as TextBox;
        var text = textBox?.Text?.Trim();
        if (string.IsNullOrWhiteSpace(text))
            return;

        AddTask(text);
        textBox?.Clear();
    }

    private void AddTask(string title)
    {
        todoBridge.AddTaskFromWidget(title);
    }

    private void ToggleTask(string taskId)
    {
        todoBridge.ToggleTaskFromWidget(taskId);
    }

    private void SelectTask(string taskId)
    {
        if (model.SelectedTaskId == taskId)
            return;

        UpdateModel(model with { SelectedTaskId = taskId });
    }

    private void UpdateTask(string taskId, Func<ReminderModel, ReminderModel> update, bool checkCelebration = false)
    {
        var current = model.Reminders.FirstOrDefault(task => task.Id == taskId);
        if (current == null)
            return;

        var next = update(current);
        if (next.Title != current.Title)
            todoBridge.UpdateTitle(taskId, next.Title ?? "");
        if (next.DueDate != current.DueDate)
            todoBridge.UpdateDueDate(taskId, next.DueDate);
        if (next.Priority != current.Priority)
            todoBridge.UpdatePriority(taskId, next.Priority);
        if (next.Category != current.Category)
            todoBridge.UpdateCategory(taskId, next.Category ?? "Uncategorized");
        if (next.Recurrence != current.Recurrence)
            todoBridge.UpdateRecurrence(taskId, next.Recurrence);
        if (next.Notes != current.Notes)
            todoBridge.UpdateNotes(taskId, next.Notes ?? "");

        UpdateModel(model with { SelectedTaskId = taskId, UpdatedAt = DateTime.Now }, checkCelebration);
    }

    private void DeleteTask(string taskId)
    {
        todoBridge.DeleteTask(taskId);
    }

    private void UndoDelete()
    {
        UpdateModel(model with { DeletedTasks = null, UpdatedAt = DateTime.Now });
    }

    private void AddSubtask(string taskId, string title)
    {
        todoBridge.AddSubtask(taskId, title);
    }

    private void UpdateSubtask(string taskId, string subtaskId, Func<ReminderSubtaskModel, ReminderSubtaskModel> update)
    {
        todoBridge.UpdateSubtask(taskId, subtaskId, update);
    }

    private void RemoveSubtask(string taskId, string subtaskId)
    {
        todoBridge.RemoveSubtask(taskId, subtaskId);
    }

    private void MoveTaskBy(string taskId, int delta)
    {
        UpdateModel(model with { SelectedTaskId = taskId, UpdatedAt = DateTime.Now });
    }

    private void MoveTaskBefore(string sourceId, string targetId, TodoSection section)
    {
        if (sourceId == targetId)
            return;

        todoBridge.MoveTaskToSection(sourceId, section);
        UpdateModel(model with { SelectedTaskId = sourceId, UpdatedAt = DateTime.Now });
    }

    private void MoveTaskToSection(string taskId, TodoSection section)
    {
        todoBridge.MoveTaskToSection(taskId, section);
        UpdateModel(model with { SelectedTaskId = taskId, UpdatedAt = DateTime.Now });
    }

    private void UpdateModel(RemindersListModel newModel, bool checkCelebration = false)
    {
        var wasIncomplete = model.Reminders.Any(task => !task.Completed);
        var settings = newModel.Normalize() with
        {
            Reminders = [],
            DeletedTasks = null
        };
        ApplyModel(todoBridge.GetWidgetModel(settings));
        var currentLayout = widgetLayoutProvider.Get();
        if (currentLayout == null)
            return;

        var layout = currentLayout with { Settings = JsonSerializer.SerializeToElement(settings, TodoStorage.JsonOptions) };

        saving = true;
        try
        {
            widgetLayoutProvider.Save(layout);
        }
        finally
        {
            saving = false;
        }

        BackupTodos();

        if (checkCelebration && wasIncomplete && model.Reminders.Count > 0 && model.Reminders.All(task => task.Completed))
            Celebrate();
    }

    private void ApplyModel(RemindersListModel newModel)
    {
        model = newModel.Normalize();
        viewModel.SetModel(model);
        ApplyWindowOptions();
    }

    private void ApplyWindowOptions()
    {
        if (VisualRoot is not Window window)
            return;

        window.Topmost = model.AlwaysOnTop;
    }

    private void Celebrate()
    {
        viewModel.Celebrating = true;
        var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            viewModel.Celebrating = false;
        };
        timer.Start();
    }

    private void BackupTodos()
    {
        if (!model.LocalStorage)
            return;

        try
        {
            if (widgetLayoutProvider.Get() == null)
                return;

            var folder = Path.Combine(Const.CurrentFolder, "TodoBackups");
            Directory.CreateDirectory(folder);
            var path = Path.Combine(folder, $"{model.Id}.json");
            File.WriteAllText(path, JsonSerializer.Serialize(model, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch
        {
            // Local backups should not interrupt desktop widget interactions.
        }
    }

    private static ReminderModel MoveToSection(ReminderModel task, TodoSection section)
    {
        var completed = section == TodoSection.Done;
        return task with
        {
            Section = section,
            Completed = completed,
            CompletedAt = completed ? DateTime.Now : null
        };
    }

    private static TodoSection SectionFromDueDate(DateTime? dueDate, TodoSection fallback) =>
        dueDate.HasValue ? dueDate.Value.Date <= DateTime.Today ? TodoSection.Today : TodoSection.Upcoming : fallback == TodoSection.Done ? TodoSection.Today : fallback;

    private static System.Collections.Generic.List<ReminderModel> WithOrders(IEnumerable<ReminderModel> reminders) =>
        reminders.Select((task, index) => task with { Order = index }).ToList();

    private static bool IsInteractive(Control? control)
    {
        while (control != null)
        {
            if (control is Button or CheckBox or TextBox or ComboBox)
                return true;

            control = control.Parent as Control;
        }

        return false;
    }
}
