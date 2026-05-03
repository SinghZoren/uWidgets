using System.Text.Json;
using Notes.Models;
using uWidgets.Core;

namespace Notes.Services;

public class NoteStorage
{
    private readonly string path;

    public static JsonSerializerOptions JsonOptions { get; } = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public NoteStorage(string? path = null)
    {
        this.path = path ?? Path.Combine(Const.CurrentFolder, "NotesData.json");
    }

    public NotesDataModel Load()
    {
        try
        {
            if (!File.Exists(path))
                return new NotesDataModel([], DateTime.Now).Normalize();

            var json = File.ReadAllText(path);
            if (string.IsNullOrWhiteSpace(json))
                return new NotesDataModel([], DateTime.Now).Normalize();

            return (JsonSerializer.Deserialize<NotesDataModel>(json, JsonOptions) ?? new NotesDataModel())
                .Normalize();
        }
        catch
        {
            return new NotesDataModel([], DateTime.Now).Normalize();
        }
    }

    public void Save(NotesDataModel data)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonSerializer.Serialize(data.Normalize(), JsonOptions));
    }
}
