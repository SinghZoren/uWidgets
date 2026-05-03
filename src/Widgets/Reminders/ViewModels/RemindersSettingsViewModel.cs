using System.Text.Json;
using ReactiveUI;
using Reminders.Models;
using uWidgets.Core;
using uWidgets.Core.Interfaces;

namespace Reminders.ViewModels;

public class RemindersSettingsViewModel : ReactiveObject
{
    private readonly IWidgetLayoutProvider widgetLayoutProvider;
    private RemindersListModel model;
    private string status = "";

    public RemindersSettingsViewModel(IWidgetLayoutProvider widgetLayoutProvider)
    {
        this.widgetLayoutProvider = widgetLayoutProvider;
        model = GetModel();
    }

    public bool IsCompact
    {
        get => model.IsCompact;
        set => Update(model with { IsCompact = value });
    }

    public bool AutoHideCompleted
    {
        get => model.AutoHideCompleted;
        set => Update(model with { AutoHideCompleted = value });
    }

    public bool FocusMode
    {
        get => model.FocusMode;
        set => Update(model with { FocusMode = value });
    }

    public bool DailyReset
    {
        get => model.DailyReset;
        set => Update(model with { DailyReset = value, LastDailyReset = value ? DateTime.Today : model.LastDailyReset });
    }

    public bool LockLayout
    {
        get => model.LockLayout;
        set => Update(model with { LockLayout = value });
    }

    public bool AlwaysOnTop
    {
        get => model.AlwaysOnTop;
        set => Update(model with { AlwaysOnTop = value });
    }

    public bool LocalStorage
    {
        get => model.LocalStorage;
        set => Update(model with { LocalStorage = value });
    }

    public bool UseCategoryColors
    {
        get => model.UseCategoryColors;
        set => Update(model with { UseCategoryColors = value });
    }

    public string Status
    {
        get => status;
        private set => this.RaiseAndSetIfChanged(ref status, value);
    }

    public void DeleteCompleted()
    {
        var completed = model.Reminders.Where(entry => entry.Completed).ToList();
        Update(model with
        {
            Reminders = model.Reminders.Where(entry => !entry.Completed).ToList(),
            DeletedTasks = completed,
            UpdatedAt = DateTime.Now
        });
        Status = completed.Count == 0 ? "No completed tasks to clear." : "Completed tasks cleared.";
    }

    public void DeleteAll()
    {
        Update(model with
        {
            Reminders = [],
            DeletedTasks = model.Reminders,
            SelectedTaskId = null,
            UpdatedAt = DateTime.Now
        });
        Status = "All tasks cleared.";
    }

    public void UndoDelete()
    {
        if (model.DeletedTasks is not { Count: > 0 } deleted)
        {
            Status = "Nothing to undo.";
            return;
        }

        Update(model with
        {
            Reminders = [..model.Reminders, ..deleted],
            DeletedTasks = null,
            SelectedTaskId = deleted.Last().Id,
            UpdatedAt = DateTime.Now
        });
        Status = "Deleted tasks restored.";
    }

    public void ExportJson()
    {
        try
        {
            var folder = BackupFolder();
            Directory.CreateDirectory(folder);
            var fileName = $"{SafeName(model.ListName ?? "Todo")}_{DateTime.Now:yyyyMMdd_HHmmss}.json";
            File.WriteAllText(Path.Combine(folder, fileName), JsonSerializer.Serialize(model, new JsonSerializerOptions { WriteIndented = true }));
            Status = $"Exported to TodoBackups\\{fileName}";
        }
        catch
        {
            Status = "Export failed.";
        }
    }

    public void ImportLatestJson()
    {
        try
        {
            var folder = BackupFolder();
            var latest = Directory.Exists(folder)
                ? Directory.GetFiles(folder, "*.json").OrderByDescending(File.GetLastWriteTime).FirstOrDefault()
                : null;

            if (latest == null)
            {
                Status = "No JSON backup found.";
                return;
            }

            var imported = JsonSerializer.Deserialize<RemindersListModel>(File.ReadAllText(latest));
            if (imported == null)
            {
                Status = "Import failed.";
                return;
            }

            Update(imported.Normalize() with { UpdatedAt = DateTime.Now });
            Status = $"Imported {Path.GetFileName(latest)}";
        }
        catch
        {
            Status = "Import failed.";
        }
    }

    private RemindersListModel GetModel() =>
        (widgetLayoutProvider.Get().GetModel<RemindersListModel>() ??
         new RemindersListModel("Today", [])).Normalize();

    private void Update(RemindersListModel newModel)
    {
        model = newModel.Normalize();
        var layout = widgetLayoutProvider.Get();
        widgetLayoutProvider.Save(layout with { Settings = JsonSerializer.SerializeToElement(model) });
        RaiseAll();
    }

    private void RaiseAll()
    {
        this.RaisePropertyChanged(nameof(IsCompact));
        this.RaisePropertyChanged(nameof(AutoHideCompleted));
        this.RaisePropertyChanged(nameof(FocusMode));
        this.RaisePropertyChanged(nameof(DailyReset));
        this.RaisePropertyChanged(nameof(LockLayout));
        this.RaisePropertyChanged(nameof(AlwaysOnTop));
        this.RaisePropertyChanged(nameof(LocalStorage));
        this.RaisePropertyChanged(nameof(UseCategoryColors));
    }

    private static string BackupFolder() => Path.Combine(Const.CurrentFolder, "TodoBackups");

    private static string SafeName(string value)
    {
        var safe = string.Join("_", value.Split(Path.GetInvalidFileNameChars()));
        return string.IsNullOrWhiteSpace(safe) ? "Todo" : safe;
    }
}
