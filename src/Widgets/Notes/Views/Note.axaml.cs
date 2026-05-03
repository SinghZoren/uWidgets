using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using Notes.Locales;
using Notes.Models;
using Notes.Services;
using Notes.ViewModels;
using uWidgets.Core;
using uWidgets.Core.Interfaces;
using uWidgets.Core.Models;

namespace Notes.Views;

public partial class Note : UserControl, IWidgetSettingsUpdateHandler
{
    private NoteModel model;
    private readonly IWidgetLayoutProvider widgetLayoutProvider;
    private readonly NoteStore noteStore;
    private readonly NoteViewModel viewModel;
    private NoteDocumentModel? selectedDocument;
    private bool loading;
    private bool saving;
    private bool syncingFromStore;
    private bool syncingToStore;
    private readonly DispatcherTimer obsidianRefreshTimer;

    public Note(IWidgetLayoutProvider widgetLayoutProvider)
        : this(new NoteModel(Locale.Notes_Title), widgetLayoutProvider) {}

    public Note(NoteModel model, IWidgetLayoutProvider widgetLayoutProvider)
    {
        this.model = model.Normalize();
        this.widgetLayoutProvider = widgetLayoutProvider;
        noteStore = NoteStore.Shared;
        selectedDocument = noteStore.EnsureWidgetNote(this.model);
        this.model = WithDocument(this.model, selectedDocument, this.model.ActiveTab);
        viewModel = new NoteViewModel(this.model);
        viewModel.SetDocuments(PanelDocuments());
        DataContext = viewModel;

        loading = true;
        InitializeComponent();
        loading = false;

        AttachedToVisualTree += OnAttachedToVisualTree;
        Unloaded += OnUnloaded;
        noteStore.DataChanged += OnNotesDataChanged;
        obsidianRefreshTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(10) };
        obsidianRefreshTimer.Tick += (_, _) =>
        {
            if (model.ActiveTab == NoteWidgetTab.Panel)
                viewModel.SetDocuments(PanelDocuments());
        };
        obsidianRefreshTimer.Start();
    }

    public bool TryHandleSettingsUpdate(WidgetLayout oldLayout, WidgetLayout newLayout)
    {
        var newModel = newLayout.GetModel<NoteModel>()?.Normalize();
        if (newModel == null) return false;
        if (saving || newModel == model) return true;

        selectedDocument = NoteSelectors.Find(noteStore.Data, newModel.SelectedNoteId) ??
                           noteStore.EnsureWidgetNote(newModel);
        newModel = WithDocument(newModel, selectedDocument, newModel.ActiveTab);
        ApplyModel(newModel, true, true);
        return true;
    }

    private void NoteTab_OnClick(object? sender, RoutedEventArgs e) =>
        UpdateModel(model with { ActiveTab = NoteWidgetTab.Note }, false, false);

    private void PanelTab_OnClick(object? sender, RoutedEventArgs e) =>
        UpdateModel(model with { ActiveTab = NoteWidgetTab.Panel }, false, false);

    private void OpenSelectedNote_OnClick(object? sender, RoutedEventArgs e)
    {
        var note = selectedDocument ?? PanelDocuments().FirstOrDefault();
        if (note == null)
            return;

        NoteObsidianBridge.OpenDocument(note, model);
    }

    private void PanelNewNote_OnClick(object? sender, RoutedEventArgs e) => CreatePanelNote();

    private void PanelNewNote_OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter)
            return;

        CreatePanelNote();
        e.Handled = true;
    }

    private void RecentNote_OnClick(object? sender, RoutedEventArgs e)
    {
        if ((sender as Control)?.DataContext is not NoteDocumentViewModel note)
            return;

        var document = PanelDocuments().FirstOrDefault(document => document.Id == note.Id);
        if (document == null)
            return;

        selectedDocument = document;
        NoteObsidianBridge.OpenDocument(document, model);
    }

    private void OpenObsidian_OnClick(object? sender, RoutedEventArgs e)
    {
        var note = selectedDocument ?? PanelDocuments().FirstOrDefault();
        if (note != null)
            NoteObsidianBridge.OpenDocument(note, model);
    }

    private void OnAttachedToVisualTree(object? sender, VisualTreeAttachmentEventArgs e) => ApplyWindowOptions();

    private void OnUnloaded(object? sender, RoutedEventArgs e)
    {
        AttachedToVisualTree -= OnAttachedToVisualTree;
        Unloaded -= OnUnloaded;
        noteStore.DataChanged -= OnNotesDataChanged;
        obsidianRefreshTimer.Stop();
    }

    private void Title_OnTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (loading || model.LockEditing) return;
        var text = (sender as TextBox)?.Text ?? "";
        UpdateModel(model with { Title = string.IsNullOrWhiteSpace(text) ? Locale.Notes_Title : text, Updated = DateTime.Now }, false, false);
    }

    private void Content_OnTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (loading || model.LockEditing) return;
        var textBox = (TextBox)sender!;
        var text = EnforceLimits(textBox.Text ?? "");
        if (text != textBox.Text)
        {
            loading = true;
            textBox.Text = text;
            textBox.CaretIndex = text.Length;
            loading = false;
        }

        UpdateModel(model with { Content = text, Updated = DateTime.Now }, false, false);
    }

    private void ChecklistCheckbox_OnClick(object? sender, RoutedEventArgs e)
    {
        if ((sender as CheckBox)?.DataContext is NoteLineViewModel line)
            ToggleChecklistLine(line.Index);
    }

    private void MarkdownCheckbox_OnClick(object? sender, RoutedEventArgs e)
    {
        if ((sender as CheckBox)?.DataContext is NoteLineViewModel line)
            ToggleChecklistLine(line.Index);
    }

    private void ChecklistItem_OnTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (loading || model.LockEditing) return;
        if ((sender as TextBox)?.DataContext is not NoteLineViewModel line) return;

        var text = (sender as TextBox)?.Text ?? "";
        ReplaceLine(line.Index, FormatChecklistLine(line.Completed, text), false);
    }

    private void NewTask_OnLostFocus(object? sender, RoutedEventArgs e) => CommitNewTask();

    private void NewTask_OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;
        CommitNewTask();
        e.Handled = true;
    }

    private void Root_OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!model.LockEditing || IsInteractive(e.Source as Control)) return;
        e.Handled = true;
    }

    private void ToggleChecklistLine(int index)
    {
        var lines = GetLines();
        if (index < 0 || index >= lines.Length) return;

        var current = lines[index].Trim();
        var completed = current.StartsWith("- [x] ", StringComparison.OrdinalIgnoreCase);
        var text = current.StartsWith("- [ ] ") || completed ? current[6..] : current.TrimStart('-', ' ');
        lines[index] = FormatChecklistLine(!completed, text);
        UpdateContentFromLines(lines, true);
    }

    private void ReplaceLine(int index, string value, bool refreshLists)
    {
        var lines = GetLines();
        if (index < 0 || index >= lines.Length) return;

        lines[index] = value;
        UpdateContentFromLines(lines, refreshLists);
    }

    private void CommitNewTask()
    {
        if (model.LockEditing || string.IsNullOrWhiteSpace(NewTaskBox.Text)) return;

        var lines = GetLines()
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .Append(FormatChecklistLine(false, NewTaskBox.Text!))
            .ToArray();

        loading = true;
        NewTaskBox.Text = "";
        loading = false;

        UpdateContentFromLines(lines, true);
    }

    private void UpdateContentFromLines(string[] lines, bool refreshLists)
    {
        var content = EnforceLimits(string.Join(Environment.NewLine, lines));
        UpdateModel(model with { Content = content, Updated = DateTime.Now }, refreshLists, refreshLists);
    }

    private void UpdateModel(NoteModel newModel, bool refreshText, bool refreshLists)
    {
        newModel = newModel.Normalize();
        ApplyModel(newModel, refreshText, refreshLists);
        var currentLayout = widgetLayoutProvider.Get();
        if (currentLayout == null)
            return;

        var layout = currentLayout with { Settings = JsonSerializer.SerializeToElement(model) };
        saving = true;
        try
        {
            widgetLayoutProvider.Save(layout);
        }
        finally
        {
            saving = false;
        }
        BackupNote();

        if (!syncingFromStore)
            SyncCurrentDocument();
    }

    private void ApplyModel(NoteModel newModel, bool refreshText, bool refreshLists)
    {
        model = newModel.Normalize();
        viewModel.SetModel(model, refreshText, refreshLists);
        viewModel.SetDocuments(PanelDocuments());
        if (refreshText)
            ApplyText();
        ApplyWindowOptions();
    }

    private void OnNotesDataChanged(object? sender, NotesDataModel newData)
    {
        void Update()
        {
            viewModel.SetDocuments(PanelDocuments());
            selectedDocument = NoteSelectors.Find(newData, model.SelectedNoteId);

            if (syncingToStore || selectedDocument == null)
                return;

            if (model.Title == selectedDocument.Title &&
                model.Content == selectedDocument.Content &&
                model.IsPinned == selectedDocument.IsPinned)
                return;

            syncingFromStore = true;
            try
            {
                UpdateModel(WithDocument(model, selectedDocument, model.ActiveTab), true, true);
            }
            finally
            {
                syncingFromStore = false;
            }
        }

        if (Avalonia.Threading.Dispatcher.UIThread.CheckAccess())
            Update();
        else
            Avalonia.Threading.Dispatcher.UIThread.Post(Update);
    }

    private void SyncCurrentDocument()
    {
        var noteId = model.SelectedNoteId ?? model.Id;
        if (string.IsNullOrWhiteSpace(noteId))
            return;

        syncingToStore = true;
        try
        {
            noteStore.UpdateNote(noteId, note => note with
            {
                Title = model.Title,
                Content = model.Content,
                IsPinned = model.IsPinned
            });
        }
        finally
        {
            syncingToStore = false;
        }

        selectedDocument = NoteSelectors.Find(noteStore.Data, noteId);
        if (selectedDocument != null)
            SyncDocumentToObsidian(selectedDocument, false);
    }

    private IReadOnlyList<NoteDocumentModel> PanelDocuments() =>
        NoteObsidianBridge.IsConfigured(model)
            ? NoteObsidianBridge.GetRecentNotes(model)
            : [];

    private void CreatePanelNote()
    {
        var title = PanelNewNoteBox.Text?.Trim();
        var obsidianNote = NoteObsidianBridge.CreateVaultNote(title, model);
        if (obsidianNote != null)
        {
            selectedDocument = obsidianNote;
            PanelNewNoteBox.Clear();
            viewModel.SetDocuments(PanelDocuments());
            NoteObsidianBridge.OpenDocument(obsidianNote, model);
        }
    }

    private void SyncDocumentToObsidian(NoteDocumentModel document, bool open)
    {
        if (!NoteObsidianBridge.IsConfigured(model) || string.IsNullOrWhiteSpace(model.ObsidianVaultPath))
            return;

        try
        {
            var relativePath = NoteObsidianBridge.SyncDocument(document, model.ObsidianVaultPath);
            if (!string.Equals(document.ObsidianPath, relativePath, StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrWhiteSpace(document.Id))
            {
                noteStore.SetObsidianPath(document.Id, relativePath);
                document = document with { ObsidianPath = relativePath };
            }

            if (open)
                NoteObsidianBridge.OpenDocument(document, model.ObsidianVaultPath);
        }
        catch
        {
            // Obsidian sync should never break the widget.
        }
    }

    private void ApplyText()
    {
        loading = true;
        if (TitleBox.Text != model.Title) TitleBox.Text = model.Title;
        if (ContentBox.Text != model.Content) ContentBox.Text = model.Content;
        loading = false;
    }

    private void ApplyWindowOptions()
    {
        if (VisualRoot is not Window window) return;

        window.Topmost = model.AlwaysOnTop;
        window.TransparencyLevelHint = model.UseBlur
            ? [WindowTransparencyLevel.Blur, WindowTransparencyLevel.Transparent]
            : [WindowTransparencyLevel.Transparent];

        if (!viewModel.UsesWidgetBackground && window.FindControl<Border>("Border") is { } border)
            border.Background = viewModel.BackgroundBrush;
    }

    private void BackupNote()
    {
        if (!model.LocalBackup) return;

        try
        {
            var folder = Path.Combine(Const.CurrentFolder, "NotesBackups");
            Directory.CreateDirectory(folder);
            var title = string.Join("_", (model.Title ?? "Note").Split(Path.GetInvalidFileNameChars()));
            if (string.IsNullOrWhiteSpace(title)) title = "Note";
            var stem = $"{title}_{model.Id![..8]}";
            File.WriteAllText(Path.Combine(folder, $"{stem}.txt"), model.Content ?? "");
            File.WriteAllText(
                Path.Combine(folder, $"{stem}.json"),
                JsonSerializer.Serialize(model, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch
        {
            // Backups should never interrupt typing in the widget.
        }
    }

    private string EnforceLimits(string text)
    {
        if (model.MaxLines > 0)
            text = string.Join(Environment.NewLine, text.Replace("\r\n", "\n").Split('\n').Take(model.MaxLines));

        if (model.MaxCharacters > 0 && text.Length > model.MaxCharacters)
            text = text[..model.MaxCharacters];

        return text;
    }

    private string[] GetLines() => (model.Content ?? "").Replace("\r\n", "\n").Split('\n');

    private static string FormatChecklistLine(bool completed, string text) =>
        $"- [{(completed ? "x" : " ")}] {text.Trim()}";

    private static NoteModel WithDocument(NoteModel settings, NoteDocumentModel document, NoteWidgetTab tab) =>
        settings.Normalize() with
        {
            SelectedNoteId = document.Id,
            Title = document.Title,
            Content = document.Content,
            Updated = document.UpdatedAt,
            IsPinned = document.IsPinned,
            ActiveTab = tab
        };

    private static bool IsInteractive(Control? control)
    {
        while (control != null)
        {
            if (control is Button or CheckBox or TextBox)
                return true;
            control = control.Parent as Control;
        }

        return false;
    }
}
