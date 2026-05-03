namespace Notes.Models;

public record NoteDocumentModel(
    string? Id = null,
    string? Title = null,
    string? Content = null,
    DateTime? CreatedAt = null,
    DateTime? UpdatedAt = null,
    bool IsPinned = false,
    string? ObsidianPath = null)
{
    public NoteDocumentModel Normalize(DateTime now) => this with
    {
        Id = string.IsNullOrWhiteSpace(Id) ? Guid.NewGuid().ToString("N") : Id.Trim(),
        Title = string.IsNullOrWhiteSpace(Title) ? "Untitled note" : Title.Trim(),
        Content = Content ?? "",
        CreatedAt = CreatedAt ?? now,
        UpdatedAt = UpdatedAt ?? now,
        ObsidianPath = string.IsNullOrWhiteSpace(ObsidianPath) ? null : ObsidianPath
    };
}

public record NotesDataModel(
    List<NoteDocumentModel>? Notes = null,
    DateTime? UpdatedAt = null,
    int Version = 1)
{
    public NotesDataModel Normalize()
    {
        var now = DateTime.Now;
        var notes = (Notes ?? [])
            .Select(note => note.Normalize(now))
            .GroupBy(note => note.Id)
            .Select(group => group.First())
            .OrderByDescending(note => note.IsPinned)
            .ThenByDescending(note => note.UpdatedAt)
            .ToList();

        return this with
        {
            Notes = notes,
            UpdatedAt = UpdatedAt ?? now,
            Version = Math.Max(1, Version)
        };
    }
}
