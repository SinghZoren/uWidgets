using ReactiveUI;
using Reminders.Models;
using Avalonia.Media;

namespace Reminders.ViewModels;

public enum TodoWidgetViewMode
{
    Small,
    Medium,
    Large,
    ExtraLarge
}

public class RemindersViewModel : ReactiveObject
{
    private RemindersListModel model;
    private bool celebrating;
    private TodoWidgetViewMode viewMode = TodoWidgetViewMode.Small;
    private IReadOnlyList<TodoTaskViewModel> primaryTasks = [];
    private IReadOnlyList<TodoTaskViewModel> todayTasks = [];
    private IReadOnlyList<TodoTaskViewModel> upcomingTasks = [];
    private IReadOnlyList<TodoTaskViewModel> doneTasks = [];
    private IReadOnlyList<TodoSectionViewModel> sections = [];
    private TodoTaskViewModel? activeTask;

    public RemindersViewModel(RemindersListModel model)
    {
        this.model = model.Normalize();
        RebuildComputedLists();
    }

    public string? ListName => model.ListName;
    public IEnumerable<ReminderModel> Reminders => model.Reminders;
    public int Count => model.Reminders.Count;
    public bool IsCompact => model.IsCompact;
    public bool IsExpanded => !model.IsCompact;
    public bool FocusMode => model.FocusMode;
    public bool HasUndo => model.DeletedTasks?.Count > 0;
    public string QuickAddText => FocusMode ? "Focus task" : "Add task and press Enter";
    public string ModeText => model.FocusMode ? "Focus" : model.IsCompact ? "Compact" : "Expanded";
    public string ProgressText => $"{CompletedCount}/{TotalCount} done";
    public int TotalCount => model.Reminders.Count;
    public int CompletedCount => model.Reminders.Count(task => task.Completed);
    public double ProgressValue => TotalCount == 0 ? 0 : CompletedCount * 100.0 / TotalCount;
    public string SelectedTaskId => model.SelectedTaskId ?? "";
    public TodoSection[] SectionOptions => [TodoSection.Today, TodoSection.Upcoming, TodoSection.Done];
    public TodoPriority[] PriorityOptions => [TodoPriority.Low, TodoPriority.Medium, TodoPriority.High];
    public TodoRecurrence[] RecurrenceOptions => [TodoRecurrence.None, TodoRecurrence.Daily, TodoRecurrence.Weekly, TodoRecurrence.Monthly];
    public bool IsSmallLayout => viewMode == TodoWidgetViewMode.Small;
    public bool IsMediumLayout => viewMode == TodoWidgetViewMode.Medium;
    public bool IsLargeLayout => viewMode == TodoWidgetViewMode.Large;
    public bool IsExtraLargeLayout => viewMode == TodoWidgetViewMode.ExtraLarge;
    public string RemainingText => $"{Math.Max(0, TotalCount - CompletedCount)} left";
    public string TodayCountText => $"{todayTasks.Count} today";
    public string UpcomingCountText => $"{upcomingTasks.Count} upcoming";
    public string DoneCountText => $"{doneTasks.Count} done";
    public IBrush GlassBrush => Brush("#CC111720");
    public IBrush SoftGlassBrush => Brush("#99111620");
    public IBrush PanelBrush => Brush("#12FFFFFF");
    public IBrush StrongPanelBrush => Brush("#1AFFFFFF");
    public IBrush BorderBrush => Brush("#26FFFFFF");
    public IBrush AccentBrush => Brush("#7AD9FF");
    public IBrush TextBrush => Brush("#F4F7FB");
    public IBrush MutedBrush => Brush("#A8B2C0");
    public IBrush TrackBrush => Brush("#18FFFFFF");

    public IReadOnlyList<TodoTaskViewModel> PrimaryTasks => primaryTasks;
    public IReadOnlyList<TodoTaskViewModel> TodayTasks => todayTasks;
    public IReadOnlyList<TodoTaskViewModel> UpcomingTasks => upcomingTasks;
    public IReadOnlyList<TodoTaskViewModel> DoneTasks => doneTasks;
    public TodoTaskViewModel? ActiveTask => activeTask;
    public bool HasActiveTask => activeTask != null;
    public bool HasNoActiveTask => activeTask == null;
    public bool HasPrimaryTasks => primaryTasks.Count > 0;
    public bool HasNoPrimaryTasks => primaryTasks.Count == 0;

    public bool Celebrating
    {
        get => celebrating;
        set => this.RaiseAndSetIfChanged(ref celebrating, value);
    }

    public IReadOnlyList<TodoSectionViewModel> Sections => sections;

    public void SetModel(RemindersListModel newModel)
    {
        model = newModel.Normalize();
        RebuildComputedLists();
        RaiseAll();
    }

    public void SetViewport(double width, double height)
    {
        var nextMode = (width, height) switch
        {
            ({ } w, { } h) when w >= 500 && h >= 220 => TodoWidgetViewMode.ExtraLarge,
            ({ } w, { } h) when w >= 220 && h >= 220 => TodoWidgetViewMode.Large,
            ({ } w, { } h) when w >= 220 => TodoWidgetViewMode.Medium,
            _ => TodoWidgetViewMode.Small
        };

        if (nextMode == viewMode)
            return;

        viewMode = nextMode;
        RebuildComputedLists();
        RaiseAll();
    }

    private void RebuildComputedLists()
    {
        var visibleTasks = GetVisibleTasks().ToList();

        primaryTasks = visibleTasks
            .Where(task => !task.Completed)
            .Take(viewMode == TodoWidgetViewMode.Small ? 3 : 5)
            .ToList();

        todayTasks = CreateTasks(TodoSection.Today);
        upcomingTasks = CreateTasks(TodoSection.Upcoming);
        doneTasks = CreateTasks(TodoSection.Done);
        activeTask = GetActiveTask();
        sections = CreateSections(visibleTasks);
    }

    private IReadOnlyList<TodoSectionViewModel> CreateSections(IReadOnlyCollection<TodoTaskViewModel> tasks)
    {
        if (model.FocusMode)
            return [new TodoSectionViewModel(TodoSection.Today, tasks.ToList())];

        return
        [
            CreateSection(TodoSection.Today, tasks),
            CreateSection(TodoSection.Upcoming, tasks),
            CreateSection(TodoSection.Done, tasks)
        ];
    }

    private TodoSectionViewModel CreateSection(TodoSection section, IReadOnlyCollection<TodoTaskViewModel> tasks) =>
        new(section, tasks.Where(task => task.Section == section).ToList());

    private IEnumerable<TodoTaskViewModel> GetVisibleTasks()
    {
        var ordered = model.Reminders
            .OrderBy(task => SectionRank(task.Section))
            .ThenBy(task => task.Completed)
            .ThenBy(task => task.Order)
            .ToList();

        if (model.FocusMode)
        {
            var focusTask = ordered.FirstOrDefault(task => task.Id == model.SelectedTaskId && !task.Completed) ??
                            ordered.FirstOrDefault(task => !task.Completed) ??
                            ordered.FirstOrDefault();

            return focusTask == null
                ? []
                : [CreateTask(focusTask, true)];
        }

        if (model.IsCompact)
        {
            ordered = ordered
                .Where(task => !model.AutoHideCompleted || !task.Completed)
                .OrderByDescending(task => task.DueDate?.Date < DateTime.Today)
                .ThenBy(task => SectionRank(task.Section))
                .ThenByDescending(task => task.Priority)
                .ThenBy(task => task.Order)
                .Take(3)
                .ToList();
        }

        return ordered.Select(task => CreateTask(task, task.Id == model.SelectedTaskId));
    }

    private IReadOnlyList<TodoTaskViewModel> CreateTasks(TodoSection section) => model.Reminders
        .Where(task => task.Section == section)
        .OrderBy(task => task.Completed)
        .ThenBy(task => task.Order)
        .Select(task => CreateTask(task, task.Id == model.SelectedTaskId))
        .ToList();

    private TodoTaskViewModel? GetActiveTask()
    {
        var task = model.Reminders.FirstOrDefault(entry => entry.Id == model.SelectedTaskId) ??
                   model.Reminders.FirstOrDefault(entry => !entry.Completed) ??
                   model.Reminders.FirstOrDefault();

        return task == null ? null : CreateTask(task, true);
    }

    private TodoTaskViewModel CreateTask(ReminderModel task, bool selected) =>
        new(task, selected, false, model.UseCategoryColors);

    private static int SectionRank(TodoSection section) => section switch
    {
        TodoSection.Today => 0,
        TodoSection.Upcoming => 1,
        _ => 2
    };

    private void RaiseAll()
    {
        this.RaisePropertyChanged(nameof(ListName));
        this.RaisePropertyChanged(nameof(Reminders));
        this.RaisePropertyChanged(nameof(Count));
        this.RaisePropertyChanged(nameof(IsCompact));
        this.RaisePropertyChanged(nameof(IsExpanded));
        this.RaisePropertyChanged(nameof(FocusMode));
        this.RaisePropertyChanged(nameof(HasUndo));
        this.RaisePropertyChanged(nameof(QuickAddText));
        this.RaisePropertyChanged(nameof(ModeText));
        this.RaisePropertyChanged(nameof(ProgressText));
        this.RaisePropertyChanged(nameof(TotalCount));
        this.RaisePropertyChanged(nameof(CompletedCount));
        this.RaisePropertyChanged(nameof(ProgressValue));
        this.RaisePropertyChanged(nameof(SelectedTaskId));
        this.RaisePropertyChanged(nameof(Sections));
        this.RaisePropertyChanged(nameof(IsSmallLayout));
        this.RaisePropertyChanged(nameof(IsMediumLayout));
        this.RaisePropertyChanged(nameof(IsLargeLayout));
        this.RaisePropertyChanged(nameof(IsExtraLargeLayout));
        this.RaisePropertyChanged(nameof(RemainingText));
        this.RaisePropertyChanged(nameof(TodayCountText));
        this.RaisePropertyChanged(nameof(UpcomingCountText));
        this.RaisePropertyChanged(nameof(DoneCountText));
        this.RaisePropertyChanged(nameof(GlassBrush));
        this.RaisePropertyChanged(nameof(SoftGlassBrush));
        this.RaisePropertyChanged(nameof(PanelBrush));
        this.RaisePropertyChanged(nameof(StrongPanelBrush));
        this.RaisePropertyChanged(nameof(BorderBrush));
        this.RaisePropertyChanged(nameof(AccentBrush));
        this.RaisePropertyChanged(nameof(TextBrush));
        this.RaisePropertyChanged(nameof(MutedBrush));
        this.RaisePropertyChanged(nameof(TrackBrush));
        this.RaisePropertyChanged(nameof(PrimaryTasks));
        this.RaisePropertyChanged(nameof(TodayTasks));
        this.RaisePropertyChanged(nameof(UpcomingTasks));
        this.RaisePropertyChanged(nameof(DoneTasks));
        this.RaisePropertyChanged(nameof(ActiveTask));
        this.RaisePropertyChanged(nameof(HasActiveTask));
        this.RaisePropertyChanged(nameof(HasNoActiveTask));
        this.RaisePropertyChanged(nameof(HasPrimaryTasks));
        this.RaisePropertyChanged(nameof(HasNoPrimaryTasks));
    }

    private static IBrush Brush(string color) => SolidColorBrush.Parse(color);
}
