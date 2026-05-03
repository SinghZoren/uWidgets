namespace Reminders.Models;

public record TodoSubtaskModel(
    string? Id = null,
    string? Title = null,
    bool Completed = false,
    int Order = 0)
{
    public TodoSubtaskModel Normalize(int order) => this with
    {
        Id = TodoModelHelpers.NewIdIfEmpty(Id),
        Title = Title?.Trim() ?? "",
        Order = order
    };
}

public record TodoCategoryModel(
    string? Id = null,
    string? Name = null,
    string Color = "#29323B",
    DateTime? CreatedAt = null,
    DateTime? UpdatedAt = null)
{
    public override string ToString() => Name ?? "Uncategorized";

    public TodoCategoryModel Normalize(DateTime now) => this with
    {
        Name = TodoModelHelpers.NormalizeCategoryName(Name),
        Id = string.IsNullOrWhiteSpace(Id) ? TodoModelHelpers.CategoryId(Name) : TodoModelHelpers.CategoryId(Id),
        Color = string.IsNullOrWhiteSpace(Color) ? "#29323B" : Color,
        CreatedAt = CreatedAt ?? now,
        UpdatedAt = UpdatedAt ?? now
    };
}

public record TodoTaskModel(
    string? Id = null,
    string? Title = null,
    bool Completed = false,
    DateTime? CreatedAt = null,
    DateTime? UpdatedAt = null,
    DateTime? DueDate = null,
    TimeSpan? DueTime = null,
    TodoPriority Priority = TodoPriority.None,
    string? CategoryId = null,
    string? Notes = null,
    List<TodoSubtaskModel>? Subtasks = null,
    TodoRecurrence Recurrence = TodoRecurrence.None,
    DateTime? CompletedAt = null,
    int Order = 0,
    TodoStatus Status = TodoStatus.Todo)
{
    public TodoTaskModel Normalize(int order, DateTime now)
    {
        var completed = Completed || Status == TodoStatus.Done;
        var status = completed ? TodoStatus.Done : Status == TodoStatus.Done ? TodoStatus.Todo : Status;

        return this with
        {
            Id = TodoModelHelpers.NewIdIfEmpty(Id),
            Title = string.IsNullOrWhiteSpace(Title) ? "New task" : Title.Trim(),
            Completed = completed,
            CreatedAt = CreatedAt ?? now,
            UpdatedAt = UpdatedAt ?? now,
            DueDate = DueDate?.Date,
            DueTime = DueTime is { TotalHours: >= 0, TotalHours: < 24 } ? DueTime : null,
            Priority = Priority,
            CategoryId = string.IsNullOrWhiteSpace(CategoryId) ? TodoModelHelpers.UncategorizedId : TodoModelHelpers.CategoryId(CategoryId),
            Notes = Notes ?? "",
            Subtasks = (Subtasks ?? [])
                .Select((subtask, index) => subtask.Normalize(index))
                .Where(subtask => !string.IsNullOrWhiteSpace(subtask.Title))
                .ToList(),
            Recurrence = Recurrence,
            CompletedAt = completed ? CompletedAt ?? now : null,
            Order = order,
            Status = status
        };
    }
}

public record TodoDataModel(
    List<TodoTaskModel>? Tasks = null,
    List<TodoCategoryModel>? Categories = null,
    DateTime? UpdatedAt = null,
    int Version = 1)
{
    public TodoDataModel Normalize()
    {
        var now = DateTime.Now;
        var categories = (Categories ?? [])
            .Select(category => category.Normalize(now))
            .GroupBy(category => category.Id)
            .Select(group => group.First())
            .ToList();

        if (categories.All(category => category.Id != TodoModelHelpers.UncategorizedId))
            categories.Insert(0, TodoModelHelpers.UncategorizedCategory(now));

        var tasks = (Tasks ?? [])
            .Select((task, index) => task.Normalize(index, now))
            .ToList();

        foreach (var categoryId in tasks.Select(task => task.CategoryId).Where(id => !string.IsNullOrWhiteSpace(id)).Distinct())
        {
            if (categories.All(category => category.Id != categoryId))
                categories.Add(new TodoCategoryModel(categoryId, categoryId, TodoModelHelpers.ColorForCategory(categoryId)).Normalize(now));
        }

        return this with
        {
            Tasks = tasks,
            Categories = categories,
            UpdatedAt = UpdatedAt ?? now,
            Version = Math.Max(1, Version)
        };
    }
}

public record TodoTaskDraft(
    string Title,
    DateTime? DueDate,
    TimeSpan? DueTime,
    TodoPriority Priority,
    string CategoryName,
    TodoRecurrence Recurrence,
    string? Notes = null);

public record TodoStats(
    int CompletedToday,
    int CompletedThisWeek,
    int Overdue,
    int TotalActive,
    int TotalTasks,
    int CompletedTasks,
    double CompletionRate);

public static class TodoModelHelpers
{
    public const string UncategorizedId = "uncategorized";

    public static string NewId() => Guid.NewGuid().ToString("N");

    public static string NewIdIfEmpty(string? id) => string.IsNullOrWhiteSpace(id) ? NewId() : id.Trim();

    public static TodoCategoryModel UncategorizedCategory(DateTime now) =>
        new(UncategorizedId, "Uncategorized", "#29323B", now, now);

    public static string NormalizeCategoryName(string? value)
    {
        var name = string.IsNullOrWhiteSpace(value) ? "Uncategorized" : value.Trim();
        return name.Equals("All", StringComparison.OrdinalIgnoreCase) ? "Uncategorized" : name;
    }

    public static string CategoryId(string? value)
    {
        var name = NormalizeCategoryName(value).ToLowerInvariant();
        var chars = name.Select(ch => char.IsLetterOrDigit(ch) ? ch : '-').ToArray();
        var slug = string.Join('-', new string(chars).Split('-', StringSplitOptions.RemoveEmptyEntries));
        return string.IsNullOrWhiteSpace(slug) ? UncategorizedId : slug;
    }

    public static DateTime? CombinedDueDateTime(TodoTaskModel task)
    {
        if (!task.DueDate.HasValue)
            return null;

        return task.DueDate.Value.Date + (task.DueTime ?? TimeSpan.Zero);
    }

    public static string ColorForCategory(string? categoryId) => (categoryId ?? "").ToLowerInvariant() switch
    {
        "school" => "#243452",
        "work" => "#1E3A34",
        "projects" => "#332A4A",
        "personal" => "#32343B",
        "uncategorized" => "#29323B",
        _ => "#29323B"
    };
}
