using Avalonia;
using Avalonia.Media;
using Avalonia.Styling;
using ReactiveUI;
using Notes.Models;

namespace Notes.ViewModels;

public class NoteViewModel : ReactiveObject
{
    private static readonly Color GlassTitleColor = Color.Parse("#B6C0C6");
    private static readonly Color GlassTextColor = Color.Parse("#A7B1B7");
    private static readonly Color GlassMutedColor = Color.Parse("#7F8A91");

    private NoteModel noteModel;
    private IReadOnlyList<NoteDocumentModel> documents = [];

    public NoteViewModel(NoteModel noteModel)
    {
        this.noteModel = noteModel.Normalize();
    }

    public NoteModel Model => noteModel;
    public string? Title => noteModel.Title;
    public string? Content => noteModel.Content;
    public string? Updated => noteModel.Updated?.ToString("g", Thread.CurrentThread.CurrentUICulture);
    public bool IsPinned => noteModel.IsPinned;
    public bool IsNoteTab => noteModel.ActiveTab == NoteWidgetTab.Note;
    public bool IsLibraryTab => noteModel.ActiveTab == NoteWidgetTab.Panel;
    public bool IsChecklistMode => noteModel.IsChecklistMode;
    public bool IsNormalMode => !noteModel.IsChecklistMode;
    public bool IsCompact => noteModel.IsCompact;
    public bool IsExpanded => !noteModel.IsCompact;
    public bool LockEditing => noteModel.LockEditing;
    public bool CanEdit => !noteModel.LockEditing;
    public string PinnedText => noteModel.IsPinned ? "PINNED" : "";
    public bool ShowPinnedText => noteModel.IsPinned;
    public bool UsesWidgetBackground => noteModel.BackgroundColor == "Transparent";
    public IBrush BackgroundBrush => CreateBrush(noteModel.BackgroundColor, noteModel.OpacityLevel);
    public IBrush HeaderBrush => CreateBrush(noteModel.HeaderColor, Math.Min(1, noteModel.OpacityLevel + 0.12));
    public IBrush TitleBrush => CreateGlassAwareBrush(noteModel.TextColor, GlassTitleColor);
    public IBrush TextBrush => CreateGlassAwareBrush(noteModel.TextColor, GlassTextColor);
    public IBrush MutedBrush => CreateGlassAwareBrush(noteModel.TextColor, GlassMutedColor, 0.9);
    public IBrush AccentBrush => CreateGlassAwareBrush(noteModel.AccentColor, GlassTitleColor);
    public FontFamily FontFamily => noteModel.FontFamily == "Inter"
        ? new FontFamily("avares://Avalonia.Fonts.Inter#Inter")
        : new FontFamily(noteModel.FontFamily ?? "Inter");
    public double FontSize => noteModel.FontSize;
    public double LineHeight => noteModel.FontSize * noteModel.LineSpacing;
    public TextAlignment TextAlignment => noteModel.TextAlignment switch
    {
        NoteTextAlignment.Center => TextAlignment.Center,
        NoteTextAlignment.Right => TextAlignment.Right,
        _ => TextAlignment.Left
    };
    public IEnumerable<NoteLineViewModel> MarkdownLines => ParseMarkdown(noteModel.Content).Select(ApplyLineStyle);
    public IEnumerable<NoteLineViewModel> ChecklistLines => ParseChecklist(noteModel.Content).Select(ApplyLineStyle);
    public IEnumerable<NoteDocumentViewModel> RecentNotes =>
        documents.Select(note => NoteDocumentViewModel.From(note, noteModel.SelectedNoteId));
    public bool HasRecentNotes => documents.Count > 0;
    public string ObsidianStatus => Notes.Services.NoteObsidianBridge.TryGetVaultPath(noteModel) is { } path
        ? $"Obsidian: {Path.GetFileName(path)}"
        : "Set an Obsidian vault folder in settings";
    public bool CanOpenObsidian => Notes.Services.NoteObsidianBridge.TryGetVaultPath(noteModel) != null;

    public void SetModel(NoteModel model, bool refreshText = true, bool refreshLists = true)
    {
        noteModel = model.Normalize();
        RaiseAll(refreshText, refreshLists);
    }

    public void SetDocuments(IEnumerable<NoteDocumentModel> notes)
    {
        documents = notes.ToList();
        this.RaisePropertyChanged(nameof(RecentNotes));
        this.RaisePropertyChanged(nameof(HasRecentNotes));
    }

    private void RaiseAll(bool refreshText = true, bool refreshLists = true)
    {
        this.RaisePropertyChanged(nameof(Model));
        if (refreshText)
        {
            this.RaisePropertyChanged(nameof(Title));
            this.RaisePropertyChanged(nameof(Content));
        }
        this.RaisePropertyChanged(nameof(Updated));
        this.RaisePropertyChanged(nameof(IsPinned));
        this.RaisePropertyChanged(nameof(IsNoteTab));
        this.RaisePropertyChanged(nameof(IsLibraryTab));
        this.RaisePropertyChanged(nameof(IsChecklistMode));
        this.RaisePropertyChanged(nameof(IsNormalMode));
        this.RaisePropertyChanged(nameof(IsCompact));
        this.RaisePropertyChanged(nameof(IsExpanded));
        this.RaisePropertyChanged(nameof(LockEditing));
        this.RaisePropertyChanged(nameof(CanEdit));
        this.RaisePropertyChanged(nameof(PinnedText));
        this.RaisePropertyChanged(nameof(ShowPinnedText));
        this.RaisePropertyChanged(nameof(UsesWidgetBackground));
        this.RaisePropertyChanged(nameof(BackgroundBrush));
        this.RaisePropertyChanged(nameof(HeaderBrush));
        this.RaisePropertyChanged(nameof(TitleBrush));
        this.RaisePropertyChanged(nameof(TextBrush));
        this.RaisePropertyChanged(nameof(MutedBrush));
        this.RaisePropertyChanged(nameof(AccentBrush));
        this.RaisePropertyChanged(nameof(FontFamily));
        this.RaisePropertyChanged(nameof(FontSize));
        this.RaisePropertyChanged(nameof(LineHeight));
        this.RaisePropertyChanged(nameof(TextAlignment));
        this.RaisePropertyChanged(nameof(RecentNotes));
        this.RaisePropertyChanged(nameof(HasRecentNotes));
        this.RaisePropertyChanged(nameof(ObsidianStatus));
        this.RaisePropertyChanged(nameof(CanOpenObsidian));
        if (refreshLists)
        {
            this.RaisePropertyChanged(nameof(MarkdownLines));
            this.RaisePropertyChanged(nameof(ChecklistLines));
        }
    }

    private static IBrush CreateBrush(string? colorText, double opacity)
    {
        if (colorText == "Transparent")
            return new SolidColorBrush(Colors.Transparent, opacity);

        if (colorText == "SystemForeground")
            return GetResourceBrush("SystemControlForegroundBaseHighBrush", opacity);

        if (colorText == "SystemAccent")
            return GetResourceBrush("SystemControlForegroundAccentBrush", opacity);

        if (!Color.TryParse(colorText, out var color))
            color = Colors.White;

        return new SolidColorBrush(color, opacity);
    }

    private IBrush CreateGlassAwareBrush(string? colorText, Color glassColor, double opacity = 1)
    {
        if (UsesWidgetBackground && colorText is "SystemForeground" or "SystemAccent")
            return new SolidColorBrush(glassColor, opacity);

        return CreateBrush(colorText, opacity);
    }

    private static IBrush GetResourceBrush(string key, double opacity)
    {
        if (Application.Current?.TryGetResource(key, ThemeVariant.Default, out var value) == true &&
            value is SolidColorBrush brush)
            return new SolidColorBrush(brush.Color, opacity);

        return new SolidColorBrush(Colors.White, opacity);
    }

    private NoteLineViewModel ApplyLineStyle(NoteLineViewModel line) => line with
    {
        TextBrush = TextBrush,
        AccentBrush = AccentBrush,
        FontFamily = FontFamily,
        FontSize = FontSize,
        LineHeight = LineHeight,
        TextAlignment = TextAlignment,
        LockEditing = LockEditing
    };

    private static IEnumerable<NoteLineViewModel> ParseMarkdown(string? content)
    {
        return SplitLines(content)
            .Select((line, index) => ParseMarkdownLine(line, index))
            .Where(line => !string.IsNullOrWhiteSpace(line.Text));
    }

    private static IEnumerable<NoteLineViewModel> ParseChecklist(string? content)
    {
        return SplitLines(content)
            .Select((line, index) => ParseChecklistLine(line, index))
            .Where(line => !string.IsNullOrWhiteSpace(line.Text));
    }

    private static NoteLineViewModel ParseMarkdownLine(string line, int index)
    {
        var text = line.Trim();
        var isHeading = text.StartsWith("# ");
        if (isHeading) text = text[2..].Trim();

        var checkbox = TryParseCheckbox(text, out var checkboxText, out var completed);
        if (checkbox) text = checkboxText;

        var isBullet = !checkbox && text.StartsWith("- ");
        if (isBullet) text = text[2..].Trim();

        var isBold = text.Contains("**");
        text = text.Replace("**", "");

        return new NoteLineViewModel(index, line, text, isHeading, isBullet, checkbox, completed, isBold);
    }

    private static NoteLineViewModel ParseChecklistLine(string line, int index)
    {
        var text = line.Trim();
        var completed = false;

        if (TryParseCheckbox(text, out var checkboxText, out var checkboxCompleted))
        {
            text = checkboxText;
            completed = checkboxCompleted;
        }
        else if (text.StartsWith("- "))
        {
            text = text[2..].Trim();
        }

        text = text.Replace("**", "");
        return new NoteLineViewModel(index, line, text, false, false, true, completed, false);
    }

    private static bool TryParseCheckbox(string text, out string checkboxText, out bool completed)
    {
        completed = false;
        checkboxText = text;

        if (text.StartsWith("- [ ] "))
        {
            checkboxText = text[6..].Trim();
            return true;
        }

        if (text.StartsWith("- [x] ", StringComparison.OrdinalIgnoreCase))
        {
            checkboxText = text[6..].Trim();
            completed = true;
            return true;
        }

        return false;
    }

    private static string[] SplitLines(string? content) =>
        (content ?? "").Replace("\r\n", "\n").Split('\n');
}
