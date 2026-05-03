using Reminders.Models;

namespace Reminders.Services;

public class TodoStore
{
    private static readonly Lazy<TodoStore> SharedStore = new(() => new TodoStore(new TodoStorage()));
    private readonly TodoStorage storage;

    private TodoDataModel data;

    public event EventHandler<TodoDataModel>? DataChanged;

    public static TodoStore Shared => SharedStore.Value;

    public TodoStore(TodoStorage storage)
    {
        this.storage = storage;
        data = storage.Load();
    }

    public TodoDataModel Data => data;

    public void ImportLegacyIfEmpty(RemindersListModel legacy)
    {
        if (data.Tasks is { Count: > 0 } || legacy.Reminders.Count == 0)
            return;

        var categories = legacy.Reminders
            .Select(task => TodoModelHelpers.NormalizeCategoryName(task.Category))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(name => new TodoCategoryModel(TodoModelHelpers.CategoryId(name), name, TodoModelHelpers.ColorForCategory(name)))
            .ToList();

        var tasks = legacy.Reminders.Select((task, index) =>
        {
            var dueDateTime = task.DueDate;
            return new TodoTaskModel(
                Id: task.Id,
                Title: task.Title,
                Completed: task.Completed,
                CreatedAt: task.CreatedAt,
                UpdatedAt: DateTime.Now,
                DueDate: dueDateTime?.Date,
                DueTime: dueDateTime?.TimeOfDay == TimeSpan.Zero ? null : dueDateTime?.TimeOfDay,
                Priority: task.Priority,
                CategoryId: TodoModelHelpers.CategoryId(task.Category),
                Notes: task.Notes,
                Subtasks: (task.Subtasks ?? [])
                    .Select(subtask => new TodoSubtaskModel(subtask.Id, subtask.Title, subtask.Completed, subtask.Order))
                    .ToList(),
                Recurrence: task.Recurrence,
                CompletedAt: task.CompletedAt,
                Order: index,
                Status: task.Completed ? TodoStatus.Done : TodoStatus.Todo);
        }).ToList();

        Save(data with { Tasks = tasks, Categories = categories, UpdatedAt = DateTime.Now });
    }

    public TodoTaskModel? AddTaskFromText(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return null;

        return AddTask(TodoParser.Parse(input));
    }

    public TodoTaskModel AddTask(TodoTaskDraft draft)
    {
        var category = EnsureCategory(draft.CategoryName);
        var now = DateTime.Now;
        var task = new TodoTaskModel(
            Id: TodoModelHelpers.NewId(),
            Title: draft.Title,
            Completed: false,
            CreatedAt: now,
            UpdatedAt: now,
            DueDate: draft.DueDate?.Date ?? DateTime.Today,
            DueTime: draft.DueTime,
            Priority: draft.Priority,
            CategoryId: category.Id,
            Notes: draft.Notes,
            Subtasks: [],
            Recurrence: draft.Recurrence,
            CompletedAt: null,
            Order: NextOrder(),
            Status: TodoStatus.Todo).Normalize(NextOrder(), now);

        Save(data with { Tasks = [..(data.Tasks ?? []), task], UpdatedAt = now });
        return task;
    }

    public void UpdateTask(string taskId, Func<TodoTaskModel, TodoTaskModel> update)
    {
        var now = DateTime.Now;
        var tasks = (data.Tasks ?? [])
            .Select((task, index) => task.Id == taskId ? update(task) with { UpdatedAt = now } : task)
            .Select((task, index) => task.Normalize(index, now))
            .ToList();
        Save(data with { Tasks = tasks, UpdatedAt = now });
    }

    public void ToggleTask(string taskId)
    {
        var task = data.Tasks?.FirstOrDefault(item => item.Id == taskId);
        if (task == null)
            return;

        if (task.Completed)
            ReopenTask(taskId);
        else
            CompleteTask(taskId);
    }

    public void CompleteTask(string taskId)
    {
        var source = data.Tasks?.FirstOrDefault(task => task.Id == taskId);
        if (source == null)
            return;
        if (source.Completed)
            return;

        var now = DateTime.Now;
        var nextOccurrence = source.Recurrence == TodoRecurrence.None
            ? null
            : CreateNextOccurrence(source, now);

        var tasks = (data.Tasks ?? [])
            .Select(task => task.Id == taskId
                ? task with { Completed = true, CompletedAt = now, Status = TodoStatus.Done, UpdatedAt = now }
                : task)
            .ToList();

        if (nextOccurrence != null)
            tasks.Add(nextOccurrence);

        Save(data with { Tasks = WithOrders(tasks), UpdatedAt = now });
    }

    public void ReopenTask(string taskId) => UpdateTask(taskId, task => task with
    {
        Completed = false,
        CompletedAt = null,
        Status = TodoStatus.Todo
    });

    public void SetStatus(string taskId, TodoStatus status)
    {
        if (status == TodoStatus.Done)
        {
            CompleteTask(taskId);
            return;
        }

        UpdateTask(taskId, task => task with
        {
            Completed = false,
            CompletedAt = null,
            Status = status
        });
    }

    public void DeleteTask(string taskId)
    {
        var tasks = (data.Tasks ?? []).Where(task => task.Id != taskId).ToList();
        Save(data with { Tasks = WithOrders(tasks), UpdatedAt = DateTime.Now });
    }

    public TodoTaskModel? DuplicateTask(string taskId)
    {
        var source = data.Tasks?.FirstOrDefault(task => task.Id == taskId);
        if (source == null)
            return null;

        var now = DateTime.Now;
        var copy = source with
        {
            Id = TodoModelHelpers.NewId(),
            Title = $"{source.Title} copy",
            Completed = false,
            CompletedAt = null,
            CreatedAt = now,
            UpdatedAt = now,
            Order = NextOrder(),
            Status = TodoStatus.Todo,
            Subtasks = (source.Subtasks ?? [])
                .Select((subtask, index) => subtask with { Id = TodoModelHelpers.NewId(), Completed = false, Order = index })
                .ToList()
        };

        Save(data with { Tasks = [..(data.Tasks ?? []), copy], UpdatedAt = now });
        return copy;
    }

    public void AddSubtask(string taskId, string title)
    {
        if (string.IsNullOrWhiteSpace(title))
            return;

        UpdateTask(taskId, task =>
        {
            var subtasks = task.Subtasks ?? [];
            return task with
            {
                Subtasks = [..subtasks, new TodoSubtaskModel(TodoModelHelpers.NewId(), title.Trim(), false, subtasks.Count)]
            };
        });
    }

    public void UpdateSubtask(string taskId, string subtaskId, Func<TodoSubtaskModel, TodoSubtaskModel> update)
    {
        UpdateTask(taskId, task => task with
        {
            Subtasks = (task.Subtasks ?? [])
                .Select((subtask, index) => subtask.Id == subtaskId ? update(subtask).Normalize(index) : subtask.Normalize(index))
                .Where(subtask => !string.IsNullOrWhiteSpace(subtask.Title))
                .ToList()
        });
    }

    public void DeleteSubtask(string taskId, string subtaskId)
    {
        UpdateTask(taskId, task => task with
        {
            Subtasks = (task.Subtasks ?? [])
                .Where(subtask => subtask.Id != subtaskId)
                .Select((subtask, index) => subtask with { Order = index })
                .ToList()
        });
    }

    public TodoCategoryModel EnsureCategory(string? name)
    {
        var normalizedName = TodoModelHelpers.NormalizeCategoryName(name);
        var id = TodoModelHelpers.CategoryId(normalizedName);
        var existing = data.Categories?.FirstOrDefault(category => category.Id == id);
        if (existing != null)
            return existing;

        var now = DateTime.Now;
        var category = new TodoCategoryModel(id, normalizedName, TodoModelHelpers.ColorForCategory(id), now, now).Normalize(now);
        Save(data with { Categories = [..(data.Categories ?? []), category], UpdatedAt = now });
        return category;
    }

    public void RenameCategory(string categoryId, string newName)
    {
        if (categoryId == TodoModelHelpers.UncategorizedId || string.IsNullOrWhiteSpace(newName))
            return;

        var now = DateTime.Now;
        var nextId = TodoModelHelpers.CategoryId(newName);
        var categories = (data.Categories ?? [])
            .Select(category => category.Id == categoryId ? category with { Id = nextId, Name = newName.Trim(), UpdatedAt = now } : category)
            .GroupBy(category => category.Id)
            .Select(group => group.First())
            .ToList();
        var tasks = (data.Tasks ?? [])
            .Select(task => task.CategoryId == categoryId ? task with { CategoryId = nextId, UpdatedAt = now } : task)
            .ToList();

        Save(data with { Categories = categories, Tasks = tasks, UpdatedAt = now });
    }

    public void DeleteCategory(string categoryId)
    {
        if (categoryId == TodoModelHelpers.UncategorizedId)
            return;

        var now = DateTime.Now;
        var categories = (data.Categories ?? [])
            .Where(category => category.Id != categoryId)
            .ToList();
        var tasks = (data.Tasks ?? [])
            .Select(task => task.CategoryId == categoryId ? task with { CategoryId = TodoModelHelpers.UncategorizedId, UpdatedAt = now } : task)
            .ToList();

        Save(data with { Categories = categories, Tasks = tasks, UpdatedAt = now });
    }

    public void SetCategoryColor(string categoryId, string color)
    {
        var now = DateTime.Now;
        Save(data with
        {
            Categories = (data.Categories ?? [])
                .Select(category => category.Id == categoryId ? category with { Color = color, UpdatedAt = now } : category)
                .ToList(),
            UpdatedAt = now
        });
    }

    private void Save(TodoDataModel nextData)
    {
        data = nextData.Normalize();
        storage.Save(data);
        DataChanged?.Invoke(this, data);
    }

    private int NextOrder() => (data.Tasks ?? []).Count == 0 ? 0 : (data.Tasks ?? []).Max(task => task.Order) + 1;

    private TodoTaskModel CreateNextOccurrence(TodoTaskModel source, DateTime now)
    {
        var baseDate = source.DueDate ?? now.Date;
        var nextDate = source.Recurrence switch
        {
            TodoRecurrence.Daily => baseDate.AddDays(1),
            TodoRecurrence.Weekly => baseDate.AddDays(7),
            TodoRecurrence.Monthly => baseDate.AddMonths(1),
            _ => baseDate
        };

        return source with
        {
            Id = TodoModelHelpers.NewId(),
            Completed = false,
            CompletedAt = null,
            CreatedAt = now,
            UpdatedAt = now,
            DueDate = nextDate.Date,
            Order = NextOrder(),
            Status = TodoStatus.Todo,
            Subtasks = (source.Subtasks ?? [])
                .Select((subtask, index) => subtask with { Id = TodoModelHelpers.NewId(), Completed = false, Order = index })
                .ToList()
        };
    }

    private static List<TodoTaskModel> WithOrders(IEnumerable<TodoTaskModel> tasks) =>
        tasks.Select((task, index) => task with { Order = index }).ToList();
}
