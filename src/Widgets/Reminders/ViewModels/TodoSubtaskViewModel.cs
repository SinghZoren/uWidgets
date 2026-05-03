using Reminders.Models;

namespace Reminders.ViewModels;

public class TodoSubtaskViewModel(ReminderSubtaskModel model, string taskId)
{
    public string TaskId { get; } = taskId;
    public string Id => model.Id ?? "";
    public bool Completed => model.Completed;
    public string Title => model.Title ?? "";
}
