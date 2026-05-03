using Notes.Models;

namespace Notes.ViewModels;

public record NoteDocumentViewModel(
    string Id,
    string Title,
    string Preview,
    string Updated,
    bool IsSelected,
    bool IsPinned)
{
    public static NoteDocumentViewModel From(NoteDocumentModel note, string? selectedNoteId)
    {
        var preview = FirstContentLine(note.Content);
        return new NoteDocumentViewModel(
            note.Id ?? "",
            note.Title ?? "Untitled note",
            preview,
            note.UpdatedAt?.ToString("g", Thread.CurrentThread.CurrentUICulture) ?? "",
            note.Id == selectedNoteId,
            note.IsPinned);
    }

    private static string FirstContentLine(string? content)
    {
        var line = (content ?? "")
            .Replace("\r\n", "\n")
            .Split('\n')
            .Select(value => value.Trim().TrimStart('#', '-', '[', ']', 'x', ' '))
            .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));

        return string.IsNullOrWhiteSpace(line) ? "No content yet" : line;
    }
}
