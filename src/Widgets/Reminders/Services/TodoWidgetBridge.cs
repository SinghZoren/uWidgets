using Reminders.Models;

namespace Reminders.Services;

public class TodoWidgetBridge
{
    private readonly TodoStore store;

    public TodoWidgetBridge(TodoStore store, RemindersListModel legacyModel)
    {
        this.store = store;
        store.ImportLegacyIfEmpty(legacyModel);
        store.DataChanged += (_, _) => DataChanged?.Invoke(this, EventArgs.Empty);
    }

    public event EventHandler? DataChanged;

    public RemindersListModel GetWidgetModel(RemindersListModel settings)
    {
        var data = store.Data;
        var tasks = TodoSelectors.GetAllTasks(data)
            .Select(task => ToLegacyTask(task, data))
            .ToList();

        return settings with
        {
            Reminders = tasks,
            SelectedTaskId = tasks.Any(task => task.Id == settings.SelectedTaskId)
                ? settings.SelectedTaskId
                : tasks.FirstOrDefault(task => !task.Completed)?.Id,
            UpdatedAt = data.UpdatedAt
        };
    }

    public void AddTaskFromWidget(string title) => store.AddTaskFromText(title);

    public void ToggleTaskFromWidget(string id) => store.ToggleTask(id);

    public void DeleteTask(string id) => store.DeleteTask(id);

    public void UpdateTitle(string id, string title) =>
        store.UpdateTask(id, task => task with { Title = title });

    public void UpdateDueDate(string id, DateTime? dueDate) =>
        store.UpdateTask(id, task => task with
        {
            DueDate = dueDate?.Date,
            DueTime = dueDate?.TimeOfDay == TimeSpan.Zero ? null : dueDate?.TimeOfDay
        });

    public void UpdatePriority(string id, TodoPriority priority) =>
        store.UpdateTask(id, task => task with { Priority = priority });

    public void UpdateCategory(string id, string category)
    {
        var entry = store.EnsureCategory(category);
        store.UpdateTask(id, task => task with { CategoryId = entry.Id });
    }

    public void UpdateRecurrence(string id, TodoRecurrence recurrence) =>
        store.UpdateTask(id, task => task with { Recurrence = recurrence });

    public void UpdateNotes(string id, string notes) =>
        store.UpdateTask(id, task => task with { Notes = notes });

    public void MoveTaskToSection(string id, TodoSection section)
    {
        switch (section)
        {
            case TodoSection.Done:
                store.SetStatus(id, TodoStatus.Done);
                break;
            case TodoSection.Upcoming:
                store.SetStatus(id, TodoStatus.Todo);
                store.UpdateTask(id, task => task with
                {
                    DueDate = task.DueDate.HasValue && task.DueDate.Value.Date > DateTime.Today
                        ? task.DueDate
                        : DateTime.Today.AddDays(1)
                });
                break;
            default:
                store.SetStatus(id, TodoStatus.Todo);
                store.UpdateTask(id, task => task with { DueDate = DateTime.Today });
                break;
        }
    }

    public void AddSubtask(string taskId, string title) => store.AddSubtask(taskId, title);

    public void UpdateSubtask(string taskId, string subtaskId, Func<ReminderSubtaskModel, ReminderSubtaskModel> update)
    {
        store.UpdateSubtask(taskId, subtaskId, subtask =>
        {
            var legacy = new ReminderSubtaskModel(subtask.Id, subtask.Completed, subtask.Title, subtask.Order);
            var changed = update(legacy);
            return subtask with { Title = changed.Title, Completed = changed.Completed, Order = changed.Order };
        });
    }

    public void RemoveSubtask(string taskId, string subtaskId) => store.DeleteSubtask(taskId, subtaskId);

    public static ReminderModel ToLegacyTask(TodoTaskModel task, TodoDataModel data)
    {
        var category = data.Categories?.FirstOrDefault(entry => entry.Id == task.CategoryId)?.Name ?? "Uncategorized";
        return new ReminderModel(
            Completed: task.Completed,
            Title: task.Title,
            Id: task.Id,
            Section: ToLegacySection(task),
            DueDate: TodoModelHelpers.CombinedDueDateTime(task),
            Recurrence: task.Recurrence,
            Priority: task.Priority == TodoPriority.None ? TodoPriority.Medium : task.Priority,
            Category: category,
            Notes: task.Notes,
            Subtasks: (task.Subtasks ?? [])
                .Select(subtask => new ReminderSubtaskModel(subtask.Id, subtask.Completed, subtask.Title, subtask.Order))
                .ToList(),
            Order: task.Order,
            CreatedAt: task.CreatedAt,
            CompletedAt: task.CompletedAt);
    }

    private static TodoSection ToLegacySection(TodoTaskModel task)
    {
        if (task.Completed || task.Status == TodoStatus.Done)
            return TodoSection.Done;

        return task.DueDate.HasValue && task.DueDate.Value.Date > DateTime.Today
            ? TodoSection.Upcoming
            : TodoSection.Today;
    }
}
