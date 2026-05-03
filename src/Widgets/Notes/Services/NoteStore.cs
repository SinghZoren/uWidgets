using Notes.Models;

namespace Notes.Services;

public class NoteStore
{
    private static readonly Lazy<NoteStore> SharedStore = new(() => new NoteStore(new NoteStorage()));
    private readonly NoteStorage storage;
    private NotesDataModel data;

    public event EventHandler<NotesDataModel>? DataChanged;

    public static NoteStore Shared => SharedStore.Value;

    public NoteStore(NoteStorage storage)
    {
        this.storage = storage;
        data = storage.Load();
    }

    public NotesDataModel Data => data;

    public NoteDocumentModel EnsureWidgetNote(NoteModel model)
    {
        var normalized = model.Normalize();
        var id = normalized.SelectedNoteId ?? normalized.Id ?? Guid.NewGuid().ToString("N");
        var existing = data.Notes?.FirstOrDefault(note => note.Id == id);
        if (existing != null)
            return existing;

        var now = DateTime.Now;
        var note = new NoteDocumentModel(
            Id: id,
            Title: normalized.Title,
            Content: normalized.Content,
            CreatedAt: normalized.Updated ?? now,
            UpdatedAt: normalized.Updated ?? now,
            IsPinned: normalized.IsPinned)
            .Normalize(now);

        Save(data with { Notes = [..(data.Notes ?? []), note], UpdatedAt = now });
        return note;
    }

    public NoteDocumentModel AddNote(string? title = null, string? content = null)
    {
        var now = DateTime.Now;
        var note = new NoteDocumentModel(
            Id: Guid.NewGuid().ToString("N"),
            Title: string.IsNullOrWhiteSpace(title) ? "Untitled note" : title.Trim(),
            Content: content ?? "",
            CreatedAt: now,
            UpdatedAt: now)
            .Normalize(now);

        Save(data with { Notes = [note, ..(data.Notes ?? [])], UpdatedAt = now });
        return note;
    }

    public void UpdateNote(string noteId, Func<NoteDocumentModel, NoteDocumentModel> update)
    {
        if (string.IsNullOrWhiteSpace(noteId))
            return;

        var now = DateTime.Now;
        var notes = (data.Notes ?? [])
            .Select(note => note.Id == noteId ? update(note) with { UpdatedAt = now } : note)
            .Select(note => note.Normalize(now))
            .ToList();

        Save(data with { Notes = notes, UpdatedAt = now });
    }

    public void SetObsidianPath(string noteId, string relativePath)
    {
        if (string.IsNullOrWhiteSpace(noteId) || string.IsNullOrWhiteSpace(relativePath))
            return;

        var now = DateTime.Now;
        var notes = (data.Notes ?? [])
            .Select(note => note.Id == noteId ? note with { ObsidianPath = relativePath } : note)
            .Select(note => note.Normalize(now))
            .ToList();

        Save(data with { Notes = notes, UpdatedAt = now });
    }

    public void DeleteNote(string noteId)
    {
        if (string.IsNullOrWhiteSpace(noteId))
            return;

        var notes = (data.Notes ?? []).Where(note => note.Id != noteId).ToList();
        Save(data with { Notes = notes, UpdatedAt = DateTime.Now });
    }

    private void Save(NotesDataModel nextData)
    {
        data = nextData.Normalize();
        storage.Save(data);
        DataChanged?.Invoke(this, data);
    }
}
