using Reminders.Models;

namespace Reminders.Services;

public static class TodoSelectors
{
    public static IReadOnlyList<TodoTaskModel> GetTodayTasks(TodoDataModel data)
    {
        var today = DateTime.Today;
        return Ordered(data.Tasks ?? [])
            .Where(task => !task.Completed && task.DueDate.HasValue && task.DueDate.Value.Date <= today)
            .ToList();
    }

    public static IReadOnlyList<TodoTaskModel> GetUpcomingTasks(TodoDataModel data) =>
        Ordered(data.Tasks ?? [])
            .Where(task => !task.Completed && task.DueDate.HasValue && task.DueDate.Value.Date > DateTime.Today)
            .ToList();

    public static IReadOnlyList<TodoTaskModel> GetOverdueTasks(TodoDataModel data) =>
        Ordered(data.Tasks ?? [])
            .Where(IsOverdue)
            .ToList();

    public static IReadOnlyList<TodoTaskModel> GetAllTasks(TodoDataModel data) => Ordered(data.Tasks ?? []).ToList();

    public static TodoStats GetTodoStats(TodoDataModel data)
    {
        var tasks = data.Tasks ?? [];
        var startOfWeek = DateTime.Today.AddDays(-(int)DateTime.Today.DayOfWeek);
        var completed = tasks.Count(task => task.Completed);
        var total = tasks.Count;

        return new TodoStats(
            CompletedToday: tasks.Count(task => task.CompletedAt?.Date == DateTime.Today),
            CompletedThisWeek: tasks.Count(task => task.CompletedAt?.Date >= startOfWeek),
            Overdue: tasks.Count(IsOverdue),
            TotalActive: tasks.Count(task => !task.Completed),
            TotalTasks: total,
            CompletedTasks: completed,
            CompletionRate: total == 0 ? 0 : completed * 100.0 / total);
    }

    public static IEnumerable<TodoTaskModel> Search(
        TodoDataModel data,
        string? searchText,
        string? categoryId = null)
    {
        var categories = (data.Categories ?? []).ToDictionary(category => category.Id ?? "", category => category.Name ?? "");
        IEnumerable<TodoTaskModel> tasks = Ordered(data.Tasks ?? []);

        if (!string.IsNullOrWhiteSpace(categoryId) && categoryId != "all")
            tasks = tasks.Where(task => task.CategoryId == categoryId);

        if (string.IsNullOrWhiteSpace(searchText))
            return tasks;

        var query = searchText.Trim();
        return tasks.Where(task =>
            Contains(task.Title, query) ||
            Contains(task.Notes, query) ||
            Contains(task.CategoryId, query) ||
            (task.CategoryId != null && categories.TryGetValue(task.CategoryId, out var categoryName) && Contains(categoryName, query)) ||
            (task.Subtasks ?? []).Any(subtask => Contains(subtask.Title, query)));
    }

    public static bool IsOverdue(TodoTaskModel task) =>
        !task.Completed && task.DueDate.HasValue && task.DueDate.Value.Date < DateTime.Today;

    public static IEnumerable<TodoTaskModel> Ordered(IEnumerable<TodoTaskModel> tasks) =>
        tasks.OrderBy(task => task.Completed)
            .ThenBy(task => task.Status == TodoStatus.Done ? 2 : task.Status == TodoStatus.InProgress ? 1 : 0)
            .ThenByDescending(IsOverdue)
            .ThenByDescending(task => task.Priority)
            .ThenBy(task => task.DueDate ?? DateTime.MaxValue)
            .ThenBy(task => task.DueTime ?? TimeSpan.Zero)
            .ThenBy(task => task.Order);

    private static bool Contains(string? source, string query) =>
        !string.IsNullOrWhiteSpace(source) && source.Contains(query, StringComparison.OrdinalIgnoreCase);
}
