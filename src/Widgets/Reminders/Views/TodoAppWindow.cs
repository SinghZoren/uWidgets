using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Reminders.Models;
using Reminders.Services;
using uWidgets.Core.Interfaces;

namespace Reminders.Views;

internal class TodoAppWindow : Window
{
    private const string ViewToday = "Today";
    private const string ViewUpcoming = "Upcoming";
    private const string ViewAll = "All";
    private const string ViewBoard = "Board";
    private const string ViewCalendar = "Calendar";
    private const string ViewStats = "Stats";
    private const string AllCategories = "all";
    private static readonly string[] CategoryColorPresets =
    [
        "#29323B",
        "#243452",
        "#1E3A34",
        "#332A4A",
        "#3B3320",
        "#3D2329"
    ];

    private readonly TodoStore store = TodoStore.Shared;
    private TodoDataModel data;
    private TodoTaskModel? selectedTask;
    private string currentView = ViewToday;
    private string categoryFilter = AllCategories;
    private string searchText = "";
    private bool updatingUi;

    private TextBox? quickAddInput;
    private TextBox? searchInput;
    private ComboBox? categoryFilterBox;
    private TextBlock? titleText;
    private TextBlock? subtitleText;
    private TextBlock? progressText;
    private ProgressBar? progressBar;
    private StackPanel? navStack;
    private StackPanel? categoryStack;
    private StackPanel? categoryManagerStack;
    private StackPanel? mainContent;
    private StackPanel? detailContent;

    public TodoAppWindow(RemindersListModel legacyModel, IWidgetLayoutProvider widgetLayoutProvider)
    {
        store.ImportLegacyIfEmpty(legacyModel);
        data = store.Data;
        selectedTask = data.Tasks?.FirstOrDefault(task => task.Id == legacyModel.SelectedTaskId) ??
                       data.Tasks?.FirstOrDefault(task => !task.Completed);

        Title = "Todo";
        Width = 1080;
        Height = 690;
        MinWidth = 940;
        MinHeight = 600;
        Background = Brushes.Transparent;
        TransparencyLevelHint =
        [
            WindowTransparencyLevel.AcrylicBlur,
            WindowTransparencyLevel.Blur,
            WindowTransparencyLevel.Transparent
        ];

        KeyDown += Window_OnKeyDown;
        store.DataChanged += OnTodoDataChanged;
        Closed += OnClosed;

        BuildShell();
        RefreshAll();
    }

    private void BuildShell()
    {
        var rootGrid = new Grid { ColumnDefinitions = new ColumnDefinitions("220,*,315") };
        rootGrid.Children.Add(CreateSidebar());
        rootGrid.Children.Add(CreateMainPane());
        rootGrid.Children.Add(CreateDetailsPane());

        Content = new Border
        {
            CornerRadius = new CornerRadius(20),
            BorderBrush = Brush("#24FFFFFF"),
            BorderThickness = new Thickness(1),
            Background = Brush("#E80D0F10"),
            Child = rootGrid
        };
    }

    private Control CreateSidebar()
    {
        navStack = new StackPanel { Spacing = 7 };
        categoryStack = new StackPanel { Spacing = 6 };
        categoryManagerStack = new StackPanel { Spacing = 7 };

        var sidebar = new StackPanel
        {
            Spacing = 14,
            Children =
            {
                new TextBlock
                {
                    Text = "Todo",
                    FontSize = 25,
                    FontWeight = FontWeight.SemiBold,
                    Foreground = Brush("#F6F8FA")
                },
                new TextBlock
                {
                    Text = "Fast capture, calm planning",
                    FontSize = 12,
                    Foreground = Brush("#9AA7B5"),
                    Margin = new Thickness(0, -14, 0, 2)
                },
                navStack,
                Divider(),
                SectionHeader("Categories"),
                categoryStack,
                SectionHeader("Manage"),
                categoryManagerStack,
                new Border
                {
                    Background = Brush("#10FFFFFF"),
                    BorderBrush = Brush("#18FFFFFF"),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(12),
                    Padding = new Thickness(12),
                    Child = new TextBlock
                    {
                        Text = "Try: Finish report tomorrow 5pm #school !high",
                        TextWrapping = TextWrapping.Wrap,
                        FontSize = 12,
                        LineHeight = 18,
                        Foreground = Brush("#A8B8C7")
                    }
                }
            }
        };

        var shell = new Border
        {
            Background = Brush("#2415171A"),
            BorderBrush = Brush("#18FFFFFF"),
            BorderThickness = new Thickness(0, 0, 1, 0),
            Padding = new Thickness(18, 22),
            Child = new ScrollViewer
            {
                Content = sidebar,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
            }
        };

        Grid.SetColumn(shell, 0);
        return shell;
    }

    private Control CreateMainPane()
    {
        var pane = new Grid { RowDefinitions = new RowDefinitions("Auto,Auto,Auto,*") };
        titleText = new TextBlock { FontSize = 25, FontWeight = FontWeight.SemiBold, Foreground = Brush("#F7FAFC") };
        subtitleText = new TextBlock { FontSize = 13, Foreground = Brush("#94A3B2") };
        progressText = new TextBlock
        {
            FontSize = 12,
            FontWeight = FontWeight.SemiBold,
            Foreground = Brush("#B2BDC4"),
            HorizontalAlignment = HorizontalAlignment.Right
        };
        progressBar = new ProgressBar
        {
            Width = 170,
            Height = 5,
            Minimum = 0,
            Maximum = 100,
            Background = Brush("#18FFFFFF"),
            Foreground = Brush("#AAB5BC")
        };

        var header = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Margin = new Thickness(0, 0, 0, 16) };
        header.Children.Add(new StackPanel { Spacing = 2, Children = { titleText, subtitleText } });
        var headerStats = new StackPanel { Spacing = 6, VerticalAlignment = VerticalAlignment.Center, Children = { progressText, progressBar } };
        Grid.SetColumn(headerStats, 1);
        header.Children.Add(headerStats);
        pane.Children.Add(header);

        quickAddInput = Field("", "Add naturally: Finish report tomorrow 5pm #school !high", 15, 44);
        quickAddInput.KeyDown += QuickAdd_OnKeyDown;
        Grid.SetRow(quickAddInput, 1);
        pane.Children.Add(quickAddInput);

        searchInput = Field("", "Search tasks, notes, categories", 14, 38);
        searchInput.TextChanged += (_, _) =>
        {
            if (updatingUi) return;
            searchText = searchInput.Text?.Trim() ?? "";
            RefreshMain();
        };

        categoryFilterBox = Combo([], null);
        categoryFilterBox.MinHeight = 38;
        categoryFilterBox.SelectionChanged += (_, _) =>
        {
            if (updatingUi) return;
            categoryFilter = categoryFilterBox.SelectedItem is FilterOption option ? option.Id : AllCategories;
            RefreshAll();
        };

        var filters = new Grid { ColumnDefinitions = new ColumnDefinitions("*,145"), Margin = new Thickness(0, 12, 0, 14) };
        filters.Children.Add(searchInput);
        Grid.SetColumn(categoryFilterBox, 1);
        categoryFilterBox.Margin = new Thickness(10, 0, 0, 0);
        filters.Children.Add(categoryFilterBox);
        Grid.SetRow(filters, 2);
        pane.Children.Add(filters);

        mainContent = new StackPanel { Spacing = 12 };
        var scroll = new ScrollViewer
        {
            Content = mainContent,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
        };
        Grid.SetRow(scroll, 3);
        pane.Children.Add(scroll);

        var frame = new Border { Padding = new Thickness(22, 22), Child = pane };
        Grid.SetColumn(frame, 1);
        return frame;
    }

    private Control CreateDetailsPane()
    {
        detailContent = new StackPanel { Spacing = 12 };
        var pane = new Border
        {
            Background = Brush("#2415171A"),
            BorderBrush = Brush("#18FFFFFF"),
            BorderThickness = new Thickness(1, 0, 0, 0),
            Padding = new Thickness(18, 22),
            Child = new ScrollViewer
            {
                Content = detailContent,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
            }
        };

        Grid.SetColumn(pane, 2);
        return pane;
    }

    private void RefreshAll()
    {
        data = store.Data;
        selectedTask = selectedTask == null
            ? data.Tasks?.FirstOrDefault(task => task.Id == selectedTask?.Id)
            : data.Tasks?.FirstOrDefault(task => task.Id == selectedTask.Id);

        if (selectedTask == null && data.Tasks is { Count: > 0 })
            selectedTask = data.Tasks.FirstOrDefault(task => !task.Completed) ?? data.Tasks[0];

        updatingUi = true;
        try
        {
            if (searchInput != null && searchInput.Text != searchText)
                searchInput.Text = searchText;

            if (categoryFilterBox != null)
            {
                var options = CategoryFilterOptions();
                categoryFilterBox.ItemsSource = options;
                categoryFilterBox.SelectedItem = options.FirstOrDefault(option => option.Id == categoryFilter) ?? options[0];
            }
        }
        finally
        {
            updatingUi = false;
        }

        RefreshSidebar();
        RefreshMain();
        RefreshDetails();
    }

    private void RefreshSidebar()
    {
        if (navStack == null || categoryStack == null || categoryManagerStack == null)
            return;

        navStack.Children.Clear();
        navStack.Children.Add(NavButton(ViewToday, TodoSelectors.GetTodayTasks(data).Count, currentView == ViewToday));
        navStack.Children.Add(NavButton(ViewUpcoming, TodoSelectors.GetUpcomingTasks(data).Count, currentView == ViewUpcoming));
        navStack.Children.Add(NavButton(ViewAll, data.Tasks?.Count ?? 0, currentView == ViewAll));
        navStack.Children.Add(NavButton(ViewBoard, 0, currentView == ViewBoard));
        navStack.Children.Add(NavButton(ViewCalendar, (data.Tasks ?? []).Count(task => task.DueDate.HasValue && !task.Completed), currentView == ViewCalendar));
        navStack.Children.Add(NavButton(ViewStats, 0, currentView == ViewStats));

        categoryStack.Children.Clear();
        categoryStack.Children.Add(CategoryFilterButton("All", AllCategories, (data.Tasks ?? []).Count(task => !task.Completed)));
        foreach (var category in Categories())
            categoryStack.Children.Add(CategoryFilterButton(category.Name ?? "Uncategorized", category.Id ?? TodoModelHelpers.UncategorizedId, ActiveCategoryCount(category.Id)));

        categoryManagerStack.Children.Clear();
        var addCategory = Field("", "Add category", 12, 34);
        addCategory.KeyDown += (_, e) =>
        {
            if (e.Key != Key.Enter || string.IsNullOrWhiteSpace(addCategory.Text))
                return;
            store.EnsureCategory(addCategory.Text);
            addCategory.Clear();
            e.Handled = true;
        };
        categoryManagerStack.Children.Add(addCategory);

        foreach (var category in Categories())
            categoryManagerStack.Children.Add(CategoryManagerRow(category));
    }

    private void RefreshMain()
    {
        if (mainContent == null || titleText == null || subtitleText == null || progressText == null || progressBar == null)
            return;

        var stats = TodoSelectors.GetTodoStats(data);
        titleText.Text = currentView;
        subtitleText.Text = currentView == ViewStats
            ? "Consistency, completion, and task health"
            : $"{TodoSelectors.GetTodayTasks(data).Count} due today · {stats.Overdue} overdue · {FilteredTasks().Count()} visible";
        progressText.Text = stats.TotalTasks == 0 ? "No tasks yet" : $"{stats.CompletedTasks}/{stats.TotalTasks} done";
        progressBar.Value = stats.CompletionRate;

        mainContent.Children.Clear();
        mainContent.Children.Add(MetricGrid(stats));

        switch (currentView)
        {
            case ViewBoard:
                mainContent.Children.Add(BoardView());
                break;
            case ViewCalendar:
                mainContent.Children.Add(CalendarView());
                break;
            case ViewStats:
                mainContent.Children.Add(StatsView(stats));
                break;
            default:
                AddTaskList(mainContent, FilteredTasks().ToList());
                break;
        }
    }

    private void RefreshDetails()
    {
        if (detailContent == null)
            return;

        detailContent.Children.Clear();
        if (selectedTask == null)
        {
            detailContent.Children.Add(PanelTitle("Task details"));
            detailContent.Children.Add(Description("Select a task to edit notes, recurrence, subtasks, due dates, priority, category, and board status."));
            return;
        }

        var task = selectedTask;
        detailContent.Children.Add(PanelTitle("Task details"));

        var title = Field(task.Title ?? "", "Task title", 16, 40);
        title.LostFocus += (_, _) =>
        {
            if (!string.IsNullOrWhiteSpace(title.Text))
                store.UpdateTask(task.Id ?? "", item => item with { Title = title.Text.Trim() });
        };
        detailContent.Children.Add(title);

        detailContent.Children.Add(Label("Notes"));
        var notes = Field(task.Notes ?? "", "Markdown notes, links, context", 13, 120);
        notes.AcceptsReturn = true;
        notes.TextWrapping = TextWrapping.Wrap;
        notes.LostFocus += (_, _) => store.UpdateTask(task.Id ?? "", item => item with { Notes = notes.Text ?? "" });
        detailContent.Children.Add(notes);

        detailContent.Children.Add(Label("Due date"));
        var dueDate = Field(FormatDateInput(task.DueDate), "today, tomorrow, Friday, 2026-05-04", 13, 36);
        dueDate.LostFocus += (_, _) => store.UpdateTask(task.Id ?? "", item => item with { DueDate = TodoParser.ParseDate(dueDate.Text) });
        detailContent.Children.Add(dueDate);

        detailContent.Children.Add(Label("Due time"));
        var dueTime = Field(FormatTimeInput(task.DueTime), "5pm, 8:30pm, 14:00", 13, 36);
        dueTime.LostFocus += (_, _) => store.UpdateTask(task.Id ?? "", item => item with { DueTime = TodoParser.ParseTime(dueTime.Text) });
        detailContent.Children.Add(dueTime);

        detailContent.Children.Add(Label("Priority"));
        detailContent.Children.Add(Combo([TodoPriority.None, TodoPriority.Low, TodoPriority.Medium, TodoPriority.High], task.Priority, value =>
            store.UpdateTask(task.Id ?? "", item => item with { Priority = (TodoPriority)value })));

        detailContent.Children.Add(Label("Category"));
        detailContent.Children.Add(Combo(Categories().Cast<object>().ToList(), CurrentCategory(task), value =>
        {
            if (value is TodoCategoryModel category)
                store.UpdateTask(task.Id ?? "", item => item with { CategoryId = category.Id });
        }));

        var categoryInput = Field("", "Or type a new category", 13, 36);
        categoryInput.LostFocus += (_, _) =>
        {
            if (string.IsNullOrWhiteSpace(categoryInput.Text))
                return;
            var category = store.EnsureCategory(categoryInput.Text);
            store.UpdateTask(task.Id ?? "", item => item with { CategoryId = category.Id });
        };
        detailContent.Children.Add(categoryInput);

        detailContent.Children.Add(Label("Repeats"));
        detailContent.Children.Add(Combo([TodoRecurrence.None, TodoRecurrence.Daily, TodoRecurrence.Weekly, TodoRecurrence.Monthly], task.Recurrence, value =>
            store.UpdateTask(task.Id ?? "", item => item with { Recurrence = (TodoRecurrence)value })));

        detailContent.Children.Add(Label("Status"));
        detailContent.Children.Add(Combo([TodoStatus.Todo, TodoStatus.InProgress, TodoStatus.Done], task.Status, value =>
            store.SetStatus(task.Id ?? "", (TodoStatus)value)));

        detailContent.Children.Add(SubtasksEditor(task));
        detailContent.Children.Add(TaskActions(task));
    }

    private IEnumerable<TodoTaskModel> FilteredTasks()
    {
        IEnumerable<TodoTaskModel> tasks = TodoSelectors.Search(data, searchText, categoryFilter);
        tasks = currentView switch
        {
            ViewToday => tasks.Where(task => !task.Completed && task.DueDate.HasValue && task.DueDate.Value.Date <= DateTime.Today),
            ViewUpcoming => tasks.Where(task => !task.Completed && task.DueDate.HasValue && task.DueDate.Value.Date > DateTime.Today),
            ViewAll => tasks,
            _ => tasks
        };
        return TodoSelectors.Ordered(tasks);
    }

    private void AddTaskList(StackPanel stack, List<TodoTaskModel> tasks)
    {
        if (tasks.Count == 0)
        {
            stack.Children.Add(EmptyState());
            return;
        }

        foreach (var task in tasks)
            stack.Children.Add(TaskCard(task, false));
    }

    private Control BoardView()
    {
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,*,*") };
        AddBoardColumn(grid, 0, TodoStatus.Todo, "Todo");
        AddBoardColumn(grid, 1, TodoStatus.InProgress, "In Progress");
        AddBoardColumn(grid, 2, TodoStatus.Done, "Done");
        return grid;
    }

    private void AddBoardColumn(Grid grid, int column, TodoStatus status, string title)
    {
        var stack = new StackPanel { Spacing = 8 };
        stack.Children.Add(new TextBlock { Text = title, FontSize = 15, FontWeight = FontWeight.SemiBold, Foreground = Brush("#DCE5EC") });

        var tasks = TodoSelectors.Search(data, searchText, categoryFilter)
            .Where(task => task.Status == status)
            .OrderBy(task => task.Order)
            .ToList();

        if (tasks.Count == 0)
            stack.Children.Add(Empty("No tasks"));
        else
            foreach (var task in tasks)
                stack.Children.Add(TaskCard(task, true));

        var card = new Border
        {
            Background = Brush("#08FFFFFF"),
            BorderBrush = Brush("#16FFFFFF"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(16),
            Padding = new Thickness(12),
            Margin = new Thickness(column == 0 ? 0 : 8, 0, 0, 0),
            Child = stack
        };
        Grid.SetColumn(card, column);
        grid.Children.Add(card);
    }

    private Control CalendarView()
    {
        var stack = new StackPanel { Spacing = 10 };
        var groups = TodoSelectors.Search(data, searchText, categoryFilter)
            .Where(task => !task.Completed && task.DueDate.HasValue)
            .GroupBy(task => task.DueDate!.Value.Date)
            .OrderBy(group => group.Key)
            .ToList();

        if (groups.Count == 0)
            return Empty("No dated tasks yet.");

        foreach (var group in groups)
        {
            stack.Children.Add(new TextBlock
            {
                Text = FormatDayHeader(group.Key),
                FontSize = 15,
                FontWeight = FontWeight.SemiBold,
                Foreground = Brush("#DCE5EC"),
                Margin = new Thickness(0, 8, 0, 0)
            });
            foreach (var task in group.OrderBy(task => task.DueTime ?? TimeSpan.Zero).ThenByDescending(task => task.Priority))
                stack.Children.Add(TaskCard(task, false));
        }
        return stack;
    }

    private Control StatsView(TodoStats stats)
    {
        var stack = new StackPanel { Spacing = 12 };
        stack.Children.Add(StatBar("Completion rate", stats.CompletedTasks, stats.TotalTasks, "#89D8C8"));
        stack.Children.Add(StatBar("Completed today", stats.CompletedToday, Math.Max(1, stats.TotalTasks), "#D9C76C"));
        stack.Children.Add(StatBar("Completed this week", stats.CompletedThisWeek, Math.Max(1, stats.TotalTasks), "#9FB7FF"));
        stack.Children.Add(StatBar("Overdue", stats.Overdue, Math.Max(1, stats.TotalActive), "#FF8A8A"));
        stack.Children.Add(PanelTitle("By category"));
        foreach (var category in Categories())
        {
            var tasks = (data.Tasks ?? []).Where(task => task.CategoryId == category.Id).ToList();
            stack.Children.Add(StatBar(category.Name ?? "Uncategorized", tasks.Count(task => task.Completed), tasks.Count, category.Color));
        }
        return stack;
    }

    private Control TaskCard(TodoTaskModel task, bool showStatusButtons)
    {
        var category = CurrentCategory(task);
        var selected = selectedTask?.Id == task.Id;
        var overdue = TodoSelectors.IsOverdue(task);
        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"), RowDefinitions = new RowDefinitions("Auto,Auto,Auto") };

        var check = new CheckBox { IsChecked = task.Completed, Margin = new Thickness(0, 2, 10, 0), VerticalAlignment = VerticalAlignment.Top };
        check.Click += (_, _) => store.ToggleTask(task.Id ?? "");
        row.Children.Add(check);

        var title = new TextBlock
        {
            Text = task.Title,
            FontSize = 15,
            FontWeight = task.Priority == TodoPriority.High && !task.Completed ? FontWeight.SemiBold : FontWeight.Normal,
            Foreground = task.Completed ? Brush("#788590") : Brush("#F4F7FA"),
            TextDecorations = task.Completed ? TextDecorations.Strikethrough : null
        };
        Grid.SetColumn(title, 1);
        row.Children.Add(title);

        var due = new TextBlock
        {
            Text = FormatDue(task),
            FontSize = 12,
            Foreground = overdue ? Brush("#FF8A8A") : Brush("#8F9BA8"),
            FontWeight = overdue ? FontWeight.SemiBold : FontWeight.Normal,
            Margin = new Thickness(10, 0, 0, 0)
        };
        Grid.SetColumn(due, 2);
        row.Children.Add(due);

        var meta = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Margin = new Thickness(0, 7, 0, 0) };
        meta.Children.Add(Pill(category.Name ?? "Uncategorized", category.Color));
        meta.Children.Add(Pill(task.Priority.ToString(), PriorityColor(task.Priority)));
        meta.Children.Add(Pill(task.Status == TodoStatus.InProgress ? "In Progress" : task.Status.ToString(), "#223040"));
        if (overdue)
            meta.Children.Add(Pill("Overdue", "#4B2329"));
        if (task.Subtasks is { Count: > 0 } subtasks)
            meta.Children.Add(Pill($"{subtasks.Count(item => item.Completed)}/{subtasks.Count} subtasks", "#243A35"));
        Grid.SetRow(meta, 1);
        Grid.SetColumn(meta, 1);
        Grid.SetColumnSpan(meta, 2);
        row.Children.Add(meta);

        if (showStatusButtons)
        {
            var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Margin = new Thickness(0, 8, 0, 0) };
            buttons.Children.Add(StatusButton("Todo", task.Id, TodoStatus.Todo));
            buttons.Children.Add(StatusButton("Doing", task.Id, TodoStatus.InProgress));
            buttons.Children.Add(StatusButton("Done", task.Id, TodoStatus.Done));
            Grid.SetRow(buttons, 2);
            Grid.SetColumn(buttons, 1);
            Grid.SetColumnSpan(buttons, 2);
            row.Children.Add(buttons);
        }

        var border = new Border
        {
            Background = selected ? Brush("#169AA7AE") : Brush("#08FFFFFF"),
            BorderBrush = overdue ? Brush("#64FF8A8A") : selected ? Brush("#509AA7AE") : Brush("#18FFFFFF"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(13),
            Padding = new Thickness(12),
            Opacity = task.Completed ? 0.64 : 1,
            Child = row
        };
        border.PointerPressed += (_, e) =>
        {
            if (e.Source is CheckBox or Button)
                return;
            selectedTask = task;
            RefreshDetails();
        };
        return border;
    }

    private Control SubtasksEditor(TodoTaskModel task)
    {
        var stack = new StackPanel { Spacing = 8 };
        stack.Children.Add(Label("Subtasks"));
        foreach (var subtask in task.Subtasks ?? [])
        {
            var row = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto") };
            var check = new CheckBox { IsChecked = subtask.Completed, Margin = new Thickness(0, 3, 8, 0) };
            check.Click += (_, _) => store.UpdateSubtask(task.Id ?? "", subtask.Id ?? "", item => item with { Completed = !item.Completed });
            row.Children.Add(check);

            var title = Field(subtask.Title ?? "", "Subtask", 13, 34);
            title.Margin = new Thickness(8, 0);
            title.LostFocus += (_, _) =>
            {
                if (string.IsNullOrWhiteSpace(title.Text))
                    store.DeleteSubtask(task.Id ?? "", subtask.Id ?? "");
                else
                    store.UpdateSubtask(task.Id ?? "", subtask.Id ?? "", item => item with { Title = title.Text.Trim() });
            };
            Grid.SetColumn(title, 1);
            row.Children.Add(title);

            var delete = SmallButton("Del", () => store.DeleteSubtask(task.Id ?? "", subtask.Id ?? ""));
            Grid.SetColumn(delete, 2);
            row.Children.Add(delete);
            stack.Children.Add(row);
        }

        var add = Field("", "Add subtask and press Enter", 13, 34);
        add.KeyDown += (_, e) =>
        {
            if (e.Key != Key.Enter || string.IsNullOrWhiteSpace(add.Text))
                return;
            store.AddSubtask(task.Id ?? "", add.Text);
            add.Clear();
            e.Handled = true;
        };
        stack.Children.Add(add);
        return stack;
    }

    private Control TaskActions(TodoTaskModel task)
    {
        var stack = new StackPanel { Spacing = 8, Margin = new Thickness(0, 8, 0, 0) };
        stack.Children.Add(ActionButton(task.Completed ? "Reopen task" : "Mark complete", () => store.ToggleTask(task.Id ?? "")));
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,*") };
        grid.Children.Add(ActionButton("Duplicate", () => store.DuplicateTask(task.Id ?? "")));
        var delete = ActionButton("Delete", () => store.DeleteTask(task.Id ?? ""), true);
        delete.Margin = new Thickness(8, 0, 0, 0);
        Grid.SetColumn(delete, 1);
        grid.Children.Add(delete);
        stack.Children.Add(grid);
        return stack;
    }

    private Control CategoryManagerRow(TodoCategoryModel category)
    {
        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto,Auto"), Margin = new Thickness(0, 0, 0, 2) };
        var name = Field(category.Name ?? "Uncategorized", "Name", 11, 32);
        name.LostFocus += (_, _) =>
        {
            if (!string.IsNullOrWhiteSpace(name.Text) && name.Text != category.Name)
                store.RenameCategory(category.Id ?? "", name.Text);
        };
        row.Children.Add(name);

        var color = SmallButton("●", () => CycleCategoryColor(category));
        color.Foreground = Brush(IsValidColor(category.Color) ? category.Color : "#9AA7AE");
        color.Margin = new Thickness(6, 0);
        color.Padding = new Thickness(10, 4);
        Grid.SetColumn(color, 1);
        row.Children.Add(color);

        var delete = SmallButton("X", () => store.DeleteCategory(category.Id ?? ""));
        delete.IsEnabled = category.Id != TodoModelHelpers.UncategorizedId;
        Grid.SetColumn(delete, 2);
        row.Children.Add(delete);
        return row;
    }

    private void CycleCategoryColor(TodoCategoryModel category)
    {
        var id = category.Id ?? TodoModelHelpers.UncategorizedId;
        var current = CategoryColorPresets.ToList().FindIndex(color =>
            string.Equals(color, category.Color, StringComparison.OrdinalIgnoreCase));
        var next = CategoryColorPresets[(current + 1 + CategoryColorPresets.Length) % CategoryColorPresets.Length];
        store.SetCategoryColor(id, next);
    }

    private Control MetricGrid(TodoStats stats)
    {
        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,*,*,*"),
            RowDefinitions = new RowDefinitions("Auto"),
            Margin = new Thickness(0, 0, 0, 4)
        };
        AddMetric(grid, 0, 0, "Today", TodoSelectors.GetTodayTasks(data).Count.ToString(CultureInfo.InvariantCulture), "#89D8C8");
        AddMetric(grid, 1, 0, "Upcoming", TodoSelectors.GetUpcomingTasks(data).Count.ToString(CultureInfo.InvariantCulture), "#9FB7FF");
        AddMetric(grid, 2, 0, "Overdue", stats.Overdue.ToString(CultureInfo.InvariantCulture), "#FF8A8A");
        AddMetric(grid, 3, 0, "Completed today", stats.CompletedToday.ToString(CultureInfo.InvariantCulture), "#D9C76C");
        return grid;
    }

    private void AddMetric(Grid grid, int column, int row, string label, string value, string color)
    {
        var card = new Border
        {
            Background = Brush("#08FFFFFF"),
            BorderBrush = Brush("#14FFFFFF"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(12),
            Padding = new Thickness(10, 8),
            Margin = new Thickness(column == 0 ? 0 : 8, row == 0 ? 0 : 8, 0, 0),
            Child = new StackPanel
            {
                Spacing = 2,
                Children =
                {
                    new TextBlock { Text = value, FontSize = 18, FontWeight = FontWeight.SemiBold, Foreground = Brush(color) },
                    new TextBlock { Text = label, FontSize = 10, TextWrapping = TextWrapping.Wrap, Foreground = Brush("#8D99A5") }
                }
            }
        };
        Grid.SetColumn(card, column);
        Grid.SetRow(card, row);
        grid.Children.Add(card);
    }

    private Control StatBar(string label, int value, int total, string? color)
    {
        var percent = total <= 0 ? 0 : value * 100.0 / total;
        return new Border
        {
            Background = Brush("#08FFFFFF"),
            BorderBrush = Brush("#14FFFFFF"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(12),
            Padding = new Thickness(12),
            Child = new StackPanel
            {
                Spacing = 7,
                Children =
                {
                    new Grid
                    {
                        ColumnDefinitions = new ColumnDefinitions("*,Auto"),
                        Children =
                        {
                            new TextBlock { Text = label, FontWeight = FontWeight.SemiBold, Foreground = Brush("#DCE5EC") },
                            RightText($"{value}/{total}")
                        }
                    },
                    new ProgressBar
                    {
                        Minimum = 0,
                        Maximum = 100,
                        Value = percent,
                        Height = 6,
                        Background = Brush("#18FFFFFF"),
                        Foreground = Brush(IsValidColor(color) ? color! : "#89D8C8")
                    }
                }
            }
        };
    }

    private Button NavButton(string label, int count, bool selected) => SidebarButton(label, count, selected, () =>
    {
        currentView = label;
        searchText = "";
        RefreshAll();
    });

    private Button CategoryFilterButton(string label, string id, int count) => SidebarButton(label, count, categoryFilter == id, () =>
    {
        categoryFilter = id;
        RefreshAll();
    });

    private Button SidebarButton(string label, int count, bool selected, Action action)
    {
        var content = new Grid
        {
            Width = 174,
            ColumnDefinitions = new ColumnDefinitions("*,Auto")
        };
        content.Children.Add(new TextBlock
        {
            Text = label,
            TextTrimming = TextTrimming.CharacterEllipsis,
            FontWeight = selected ? FontWeight.SemiBold : FontWeight.Normal
        });
        var countText = new TextBlock
        {
            Text = count > 0 ? count.ToString(CultureInfo.InvariantCulture) : "",
            Foreground = Brush("#7F8C99"),
            Margin = new Thickness(10, 0, 0, 0)
        };
        Grid.SetColumn(countText, 1);
        content.Children.Add(countText);

        var button = new Button
        {
            Content = content,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            Background = selected ? Brush("#189AA7AE") : Brush("#00FFFFFF"),
            BorderBrush = selected ? Brush("#369AA7AE") : Brush("#00FFFFFF"),
            BorderThickness = new Thickness(1),
            Foreground = selected ? Brush("#F6F8FA") : Brush("#A8B4C1"),
            CornerRadius = new CornerRadius(11),
            Padding = new Thickness(10, 8)
        };
        button.Click += (_, _) => action();
        return button;
    }

    private Button StatusButton(string text, string? taskId, TodoStatus status) => SmallButton(text, () => store.SetStatus(taskId ?? "", status));

    private Button ActionButton(string text, Action action, bool destructive = false)
    {
        var button = new Button
        {
            Content = text,
            MinHeight = 38,
            Background = destructive ? Brush("#24FF6F6F") : Brush("#129AA7AE"),
            BorderBrush = destructive ? Brush("#48FF6F6F") : Brush("#2C9AA7AE"),
            BorderThickness = new Thickness(1),
            Foreground = Brush("#F6F8FA"),
            CornerRadius = new CornerRadius(11),
            Padding = new Thickness(12, 8)
        };
        button.Click += (_, _) => action();
        return button;
    }

    private Button SmallButton(string text, Action action)
    {
        var button = new Button
        {
            Content = text,
            MinHeight = 30,
            Background = Brush("#0CFFFFFF"),
            BorderBrush = Brush("#18FFFFFF"),
            BorderThickness = new Thickness(1),
            Foreground = Brush("#B5C0CB"),
            CornerRadius = new CornerRadius(9),
            Padding = new Thickness(8, 4)
        };
        button.Click += (_, _) => action();
        return button;
    }

    private TextBox Field(string text, string watermark, double fontSize, double minHeight) => new()
    {
        Text = text,
        Watermark = watermark,
        FontSize = fontSize,
        MinHeight = minHeight,
        Background = Brush("#08FFFFFF"),
        BorderBrush = Brush("#18FFFFFF"),
        BorderThickness = new Thickness(1),
        Foreground = Brush("#F5F8FA"),
        CaretBrush = Brush("#AEB8BE"),
        Padding = new Thickness(11, 7),
        CornerRadius = new CornerRadius(11)
    };

    private ComboBox Combo(IEnumerable<object> items, object? selected, Action<object>? onChange = null)
    {
        var combo = new ComboBox
        {
            ItemsSource = items,
            SelectedItem = selected,
            MinHeight = 36,
            Background = Brush("#08FFFFFF"),
            BorderBrush = Brush("#18FFFFFF"),
            BorderThickness = new Thickness(1),
            Foreground = Brush("#F5F8FA"),
            Padding = new Thickness(10, 4),
            CornerRadius = new CornerRadius(11)
        };
        combo.SelectionChanged += (_, _) =>
        {
            if (!updatingUi && combo.SelectedItem != null)
                onChange?.Invoke(combo.SelectedItem);
        };
        return combo;
    }

    private Border Pill(string text, string? color) => new()
    {
        Background = Brush(IsValidColor(color) ? color! : "#29323B"),
        CornerRadius = new CornerRadius(8),
        Padding = new Thickness(7, 2),
        Child = new TextBlock { Text = text, FontSize = 10, FontWeight = FontWeight.SemiBold, Foreground = Brush("#DDE6EC") }
    };

    private TextBlock PanelTitle(string text) => new()
    {
        Text = text,
        FontSize = 20,
        FontWeight = FontWeight.SemiBold,
        Foreground = Brush("#F7FAFC")
    };

    private TextBlock Label(string text) => new()
    {
        Text = text,
        FontSize = 12,
        FontWeight = FontWeight.SemiBold,
        Foreground = Brush("#8795A3"),
        Margin = new Thickness(0, 4, 0, -4)
    };

    private TextBlock SectionHeader(string text) => new()
    {
        Text = text,
        FontSize = 12,
        FontWeight = FontWeight.SemiBold,
        Foreground = Brush("#7F8FA0")
    };

    private TextBlock Description(string text) => new()
    {
        Text = text,
        FontSize = 13,
        LineHeight = 20,
        TextWrapping = TextWrapping.Wrap,
        Foreground = Brush("#93A1AF")
    };

    private Control EmptyState()
    {
        if (!string.IsNullOrWhiteSpace(searchText))
            return Empty("No matching tasks.");

        return currentView switch
        {
            ViewToday => Empty("No tasks due today. Add one above or plan ahead."),
            ViewUpcoming => Empty("No upcoming tasks."),
            ViewAll => Empty("No tasks yet. Try: Finish report tomorrow 5pm #school !high"),
            _ => Empty("No tasks.")
        };
    }

    private Control Empty(string text) => new Border
    {
        Background = Brush("#08FFFFFF"),
        BorderBrush = Brush("#12FFFFFF"),
        BorderThickness = new Thickness(1),
        CornerRadius = new CornerRadius(16),
        Padding = new Thickness(20),
        Child = new TextBlock
        {
            Text = text,
            HorizontalAlignment = HorizontalAlignment.Center,
            TextWrapping = TextWrapping.Wrap,
            Foreground = Brush("#7D8A97"),
            FontSize = 14
        }
    };

    private Border Divider() => new()
    {
        Height = 1,
        Background = Brush("#18FFFFFF"),
        Margin = new Thickness(0, 2)
    };

    private TextBlock RightText(string text)
    {
        var block = new TextBlock { Text = text, FontSize = 12, Foreground = Brush("#96A3B0") };
        Grid.SetColumn(block, 1);
        return block;
    }

    private void QuickAdd_OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || quickAddInput == null)
            return;

        var text = quickAddInput.Text?.Trim();
        if (string.IsNullOrWhiteSpace(text))
            return;

        var task = store.AddTaskFromText(text);
        if (task != null)
        {
            selectedTask = task;
            currentView = task.DueDate.HasValue && task.DueDate.Value.Date > DateTime.Today ? ViewUpcoming : ViewToday;
        }

        quickAddInput.Clear();
        e.Handled = true;
    }

    private void Window_OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.K && (e.KeyModifiers.HasFlag(KeyModifiers.Control) || e.KeyModifiers.HasFlag(KeyModifiers.Meta)))
        {
            quickAddInput?.Focus();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Escape)
        {
            if (!string.IsNullOrWhiteSpace(searchText))
            {
                searchText = "";
                RefreshAll();
            }
            else
            {
                selectedTask = null;
                RefreshDetails();
            }
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Delete && selectedTask != null && !IsInput(e.Source as Control))
        {
            store.DeleteTask(selectedTask.Id ?? "");
            selectedTask = null;
            e.Handled = true;
        }
    }

    private void OnTodoDataChanged(object? sender, TodoDataModel newData)
    {
        void Update()
        {
            data = newData;
            RefreshAll();
        }

        if (Avalonia.Threading.Dispatcher.UIThread.CheckAccess())
            Update();
        else
            Avalonia.Threading.Dispatcher.UIThread.Post(Update);
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        store.DataChanged -= OnTodoDataChanged;
        KeyDown -= Window_OnKeyDown;
        Closed -= OnClosed;
    }

    private List<TodoCategoryModel> Categories() =>
        (data.Categories ?? [])
        .OrderBy(category => category.Id == TodoModelHelpers.UncategorizedId ? 0 : 1)
        .ThenBy(category => category.Name)
        .ToList();

    private List<FilterOption> CategoryFilterOptions() =>
    [
        new FilterOption(AllCategories, "All"),
        ..Categories().Select(category => new FilterOption(
            category.Id ?? TodoModelHelpers.UncategorizedId,
            category.Name ?? "Uncategorized"))
    ];

    private TodoCategoryModel CurrentCategory(TodoTaskModel task) =>
        data.Categories?.FirstOrDefault(category => category.Id == task.CategoryId) ??
        TodoModelHelpers.UncategorizedCategory(DateTime.Now);

    private int ActiveCategoryCount(string? categoryId) =>
        (data.Tasks ?? []).Count(task => !task.Completed && task.CategoryId == categoryId);

    private string FormatDue(TodoTaskModel task)
    {
        if (!task.DueDate.HasValue)
            return "";

        var date = task.DueDate.Value.Date == DateTime.Today ? "Today" :
            task.DueDate.Value.Date == DateTime.Today.AddDays(1) ? "Tomorrow" :
            task.DueDate.Value.ToString("MMM d", CultureInfo.CurrentCulture);
        return task.DueTime.HasValue ? $"{date} {DateTime.Today.Add(task.DueTime.Value):h:mm tt}" : date;
    }

    private static string FormatDayHeader(DateTime day) =>
        day.Date == DateTime.Today ? "Today" :
        day.Date == DateTime.Today.AddDays(1) ? "Tomorrow" :
        day.ToString("dddd, MMM d", CultureInfo.CurrentCulture);

    private static string FormatDateInput(DateTime? date) =>
        date?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? "";

    private static string FormatTimeInput(TimeSpan? time) =>
        time.HasValue ? DateTime.Today.Add(time.Value).ToString("h:mm tt", CultureInfo.InvariantCulture) : "";

    private static string PriorityColor(TodoPriority priority) => priority switch
    {
        TodoPriority.High => "#3D2329",
        TodoPriority.Medium => "#3B3320",
        TodoPriority.Low => "#203529",
        _ => "#29323B"
    };

    private static bool IsInput(Control? control)
    {
        while (control != null)
        {
            if (control is TextBox or ComboBox)
                return true;
            control = control.Parent as Control;
        }
        return false;
    }

    private static bool IsValidColor(string? value) =>
        !string.IsNullOrWhiteSpace(value) &&
        value.Length == 7 &&
        value[0] == '#' &&
        value.Skip(1).All(Uri.IsHexDigit);

    private static IBrush Brush(string color) => SolidColorBrush.Parse(color);

    private sealed record FilterOption(string Id, string Name)
    {
        public override string ToString() => Name;
    }
}
