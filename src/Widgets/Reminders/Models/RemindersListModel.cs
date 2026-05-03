namespace Reminders.Models;

public record RemindersListModel(
    string? ListName,
    List<ReminderModel> Reminders,
    string? Id = null,
    bool IsCompact = false,
    bool AutoHideCompleted = true,
    bool FocusMode = false,
    bool DailyReset = false,
    bool LockLayout = false,
    bool AlwaysOnTop = false,
    bool LocalStorage = true,
    bool UseCategoryColors = true,
    string? SelectedTaskId = null,
    DateTime? LastDailyReset = null,
    DateTime? UpdatedAt = null,
    List<ReminderModel>? DeletedTasks = null)
{
    public RemindersListModel Normalize()
    {
        var today = DateTime.Today;
        var normalized = Reminders
            .Select((task, index) => task.Normalize(index, today))
            .ToList();

        if (DailyReset && LastDailyReset?.Date < today)
        {
            normalized = normalized
                .Select(task => task.Completed && (task.Section == TodoSection.Done || task.Recurrence == TodoRecurrence.Daily)
                    ? task with
                    {
                        Completed = false,
                        CompletedAt = null,
                        Section = task.DueDate.HasValue && task.DueDate.Value.Date > today
                            ? TodoSection.Upcoming
                            : TodoSection.Today
                    }
                    : task)
                .ToList();
        }

        var selectedTaskId = normalized.Any(task => task.Id == SelectedTaskId)
            ? SelectedTaskId
            : normalized.FirstOrDefault(task => !task.Completed)?.Id;

        return this with
        {
            Id = string.IsNullOrWhiteSpace(Id) ? Guid.NewGuid().ToString("N") : Id,
            ListName = string.IsNullOrWhiteSpace(ListName) ? "Today" : ListName.Trim(),
            Reminders = normalized,
            SelectedTaskId = selectedTaskId,
            LastDailyReset = DailyReset ? today : LastDailyReset,
            DeletedTasks = DeletedTasks?
                .Select((task, index) => task.Normalize(index, today))
                .ToList(),
            UpdatedAt = UpdatedAt ?? DateTime.Now
        };
    }
}
