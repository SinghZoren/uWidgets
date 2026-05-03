namespace Reminders.Models;

public enum TodoSection
{
    Today,
    Upcoming,
    Done
}

public enum TodoPriority
{
    None = -1,
    Low,
    Medium,
    High
}

public enum TodoStatus
{
    Todo,
    InProgress,
    Done
}

public enum TodoRecurrence
{
    None,
    Daily,
    Weekly,
    Monthly
}

public record ReminderSubtaskModel(
    string? Id = null,
    bool Completed = false,
    string? Title = null,
    int Order = 0)
{
    public ReminderSubtaskModel Normalize(int order) => this with
    {
        Id = string.IsNullOrWhiteSpace(Id) ? Guid.NewGuid().ToString("N") : Id,
        Title = Title ?? "",
        Order = order
    };
}

public record ReminderModel(
    bool Completed = false,
    string? Title = null,
    string? Id = null,
    TodoSection Section = TodoSection.Today,
    DateTime? DueDate = null,
    TodoRecurrence Recurrence = TodoRecurrence.None,
    TodoPriority Priority = TodoPriority.Medium,
    string? Category = "Personal",
    string? Notes = null,
    List<ReminderSubtaskModel>? Subtasks = null,
    int Order = 0,
    DateTime? CreatedAt = null,
    DateTime? CompletedAt = null)
{
    public ReminderModel Normalize(int order, DateTime today)
    {
        var dueDate = DueDate;
        var completed = Completed;
        var completedAt = CompletedAt;
        var section = Section;

        if (completed && Recurrence != TodoRecurrence.None && completedAt?.Date < today)
        {
            dueDate = NextDueDate(dueDate ?? completedAt.Value.Date, Recurrence, today);
            completed = false;
            completedAt = null;
        }

        if (completed)
        {
            section = TodoSection.Done;
        }
        else if (dueDate.HasValue)
        {
            section = dueDate.Value.Date <= today ? TodoSection.Today : TodoSection.Upcoming;
        }
        else if (section == TodoSection.Done)
        {
            section = TodoSection.Today;
        }

        return this with
        {
            Id = string.IsNullOrWhiteSpace(Id) ? Guid.NewGuid().ToString("N") : Id,
            Title = string.IsNullOrWhiteSpace(Title) ? "New task" : Title.Trim(),
            Completed = completed,
            CompletedAt = completedAt,
            CreatedAt = CreatedAt ?? DateTime.Now,
            DueDate = dueDate,
            Section = section,
            Category = string.IsNullOrWhiteSpace(Category) ? "Personal" : Category.Trim(),
            Notes = Notes ?? "",
            Subtasks = (Subtasks ?? [])
                .Select((subtask, index) => subtask.Normalize(index))
                .Where(subtask => !string.IsNullOrWhiteSpace(subtask.Title))
                .ToList(),
            Order = order
        };
    }

    private static DateTime NextDueDate(DateTime from, TodoRecurrence recurrence, DateTime today)
    {
        var next = from.Date;
        do
        {
            next = recurrence switch
            {
                TodoRecurrence.Daily => next.AddDays(1),
                TodoRecurrence.Weekly => next.AddDays(7),
                TodoRecurrence.Monthly => next.AddMonths(1),
                _ => today
            };
        } while (next < today);

        return next;
    }
}
