using Reminders.Models;

namespace Reminders.ViewModels;

public class TodoSectionViewModel(TodoSection section, IReadOnlyList<TodoTaskViewModel> tasks)
{
    public TodoSection Section => section;
    public string Title => section switch
    {
        TodoSection.Today => "Today",
        TodoSection.Upcoming => "Upcoming",
        _ => "Done"
    };

    public IReadOnlyList<TodoTaskViewModel> Tasks => tasks;
    public bool HasTasks => tasks.Count > 0;
    public bool IsEmpty => tasks.Count == 0;
    public string CountText => tasks.Count.ToString();
}
