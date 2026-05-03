using Notes.Models;

namespace Notes.Services;

public static class NoteSelectors
{
    public static IReadOnlyList<NoteDocumentModel> Recent(NotesDataModel data, int count = 8) =>
        (data.Notes ?? [])
        .OrderByDescending(note => note.IsPinned)
        .ThenByDescending(note => note.UpdatedAt)
        .Take(count)
        .ToList();

    public static NoteDocumentModel? Find(NotesDataModel data, string? id) =>
        string.IsNullOrWhiteSpace(id)
            ? null
            : data.Notes?.FirstOrDefault(note => note.Id == id);
}
