using Avalonia.Media;
using Reminders.Models;

namespace Reminders.ViewModels;

public class TodoTaskViewModel(ReminderModel model, bool isSelected, bool showDetails, bool useCategoryColors)
{
    private static readonly Dictionary<string, string> CategoryColors = new(StringComparer.OrdinalIgnoreCase)
    {
        ["School"] = "#4B78D8",
        ["Work"] = "#2F8A6D",
        ["Personal"] = "#B1652D",
        ["Projects"] = "#7D5BBE"
    };

    public string Id => model.Id ?? "";
    public string Title => model.Title ?? "";
    public bool Completed => model.Completed;
    public TodoSection Section => model.Section;
    public TodoPriority Priority => model.Priority;
    public TodoRecurrence Recurrence => model.Recurrence;
    public string Category => model.Category ?? "Personal";
    public string Notes => model.Notes ?? "";
    public string DueDateInput => model.DueDate.HasValue
        ? model.DueDate.Value.TimeOfDay == TimeSpan.Zero
            ? model.DueDate.Value.ToString("yyyy-MM-dd")
            : model.DueDate.Value.ToString("yyyy-MM-dd h:mm tt")
        : "";
    public string DueText => model.DueDate.HasValue
        ? model.DueDate.Value.TimeOfDay == TimeSpan.Zero
            ? model.DueDate.Value.ToString("MMM d")
            : model.DueDate.Value.ToString("MMM d h:mm tt")
        : "";
    public bool HasDueDate => model.DueDate.HasValue;
    public bool IsOverdue => !model.Completed && model.DueDate?.Date < DateTime.Today;
    public bool IsSelected => isSelected;
    public bool ShowDetails => showDetails;
    public bool HasSubtasks => Subtasks.Count > 0;
    public string SubtaskProgress => HasSubtasks ? $"{Subtasks.Count(subtask => subtask.Completed)}/{Subtasks.Count} subtasks" : "";
    public IReadOnlyList<TodoSubtaskViewModel> Subtasks { get; } =
        (model.Subtasks ?? []).Select(subtask => new TodoSubtaskViewModel(subtask, model.Id ?? "")).ToList();

    public TodoPriority[] PriorityOptions => [TodoPriority.Low, TodoPriority.Medium, TodoPriority.High];
    public TodoRecurrence[] RecurrenceOptions => [TodoRecurrence.None, TodoRecurrence.Daily, TodoRecurrence.Weekly, TodoRecurrence.Monthly];
    public TodoSection[] SectionOptions => [TodoSection.Today, TodoSection.Upcoming, TodoSection.Done];
    public string[] CategoryOptions => ["School", "Work", "Personal", "Projects"];

    public IBrush PriorityBrush => Brush(Priority switch
    {
        TodoPriority.High => "#D05050",
        TodoPriority.Medium => "#D49B31",
        TodoPriority.Low => "#6F8F6A",
        _ => "#777777"
    });

    public IBrush CategoryBrush => Brush(useCategoryColors && CategoryColors.TryGetValue(Category, out var color)
        ? color
        : "#777777");

    public IBrush RowBackgroundBrush => Brush(IsSelected ? "#1A7AD9FF" : "#0DFFFFFF");
    public IBrush BorderBrush => IsOverdue ? Brush("#D05050") : Brush(IsSelected ? "#7AD9FF" : "#26FFFFFF");
    public IBrush TitleBrush => Brush(Completed ? "#888888" : "#F7F7F7");
    public IBrush MutedBrush => Brush(IsOverdue ? "#E26969" : "#AAAAAA");
    public FontWeight TitleWeight => Priority == TodoPriority.High && !Completed ? FontWeight.SemiBold : FontWeight.Normal;
    public double RowOpacity => Completed ? 0.55 : 1.0;
    public string PriorityLabel => Priority == TodoPriority.High ? "High" : Priority == TodoPriority.Medium ? "Medium" : Priority == TodoPriority.Low ? "Low" : "None";
    public string OverdueLabel => IsOverdue ? "Overdue" : "";

    private static IBrush Brush(string color) => SolidColorBrush.Parse(color);
}
