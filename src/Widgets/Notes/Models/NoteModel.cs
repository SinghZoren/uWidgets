namespace Notes.Models;

public enum NoteTemplate
{
    None,
    ToDoList,
    DailyPlan,
    Scratchpad,
    MeetingNotes,
    ClassNotes
}

public enum NoteTextAlignment
{
    Left,
    Center,
    Right
}

public enum NoteWidgetTab
{
    Note,
    Panel
}

public record NoteModel(
    string? Title = null,
    string? Content = null,
    DateTime? Updated = null,
    string? Id = null,
    bool IsPinned = false,
    bool IsChecklistMode = false,
    bool IsCompact = false,
    bool LockEditing = false,
    bool AlwaysOnTop = false,
    bool UseBlur = true,
    bool LocalBackup = true,
    string? BackgroundColor = "Transparent",
    string? HeaderColor = "Transparent",
    string? TextColor = "SystemForeground",
    string? AccentColor = "SystemAccent",
    double OpacityLevel = 1,
    string? FontFamily = "Inter",
    double FontSize = 14,
    double LineSpacing = 1.25,
    NoteTextAlignment TextAlignment = NoteTextAlignment.Left,
    int MaxCharacters = 0,
    int MaxLines = 0,
    NoteWidgetTab ActiveTab = NoteWidgetTab.Note,
    string? SelectedNoteId = null,
    bool SyncWithObsidian = false,
    string? ObsidianVaultPath = null)
{
    public NoteModel Normalize()
    {
        var id = string.IsNullOrWhiteSpace(Id) ? Guid.NewGuid().ToString("N") : Id;

        return this with
        {
            Id = id,
            Title = string.IsNullOrWhiteSpace(Title) ? "Note" : Title,
            Content = Content ?? "",
            BackgroundColor = NormalizeColor(BackgroundColor, "Transparent", "#FFF9D8"),
            HeaderColor = NormalizeColor(HeaderColor, "Transparent", "#FFE88A"),
            TextColor = NormalizeColor(TextColor, "SystemForeground", "#1F1B10"),
            AccentColor = NormalizeColor(AccentColor, "SystemAccent", "#C48300"),
            OpacityLevel = Math.Clamp(OpacityLevel, 0.05, 1),
            FontFamily = string.IsNullOrWhiteSpace(FontFamily) ? "Inter" : FontFamily,
            FontSize = Math.Clamp(FontSize, 10, 36),
            LineSpacing = Math.Clamp(LineSpacing, 1, 2.5),
            MaxCharacters = Math.Max(0, MaxCharacters),
            MaxLines = Math.Max(0, MaxLines),
            SelectedNoteId = string.IsNullOrWhiteSpace(SelectedNoteId) ? id : SelectedNoteId,
            ObsidianVaultPath = string.IsNullOrWhiteSpace(ObsidianVaultPath) ? null : ObsidianVaultPath.Trim()
        };
    }

    private static string NormalizeColor(string? value, string fallback, params string[] legacyColors)
    {
        if (string.IsNullOrWhiteSpace(value))
            return fallback;

        var text = value.Trim();
        if (text is "Transparent" or "SystemForeground" or "SystemAccent")
            return text;

        if (!text.StartsWith("#"))
            text = $"#{text}";

        if (legacyColors.Any(color => string.Equals(color, text, StringComparison.OrdinalIgnoreCase)))
            return fallback;

        return text.Length is 7 or 9 ? text : fallback;
    }
}
