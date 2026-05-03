using System.Text.Json;
using System.Text.Json.Serialization;
using Reminders.Models;
using uWidgets.Core;

namespace Reminders.Services;

public class TodoStorage
{
    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly string filePath;

    public TodoStorage(string? filePath = null)
    {
        this.filePath = filePath ?? Path.Combine(Const.CurrentFolder, "TodoData.json");
    }

    public TodoDataModel Load()
    {
        try
        {
            if (!File.Exists(filePath))
                return new TodoDataModel().Normalize();

            var json = File.ReadAllText(filePath);
            if (string.IsNullOrWhiteSpace(json))
                return new TodoDataModel().Normalize();

            return (JsonSerializer.Deserialize<TodoDataModel>(json, JsonOptions) ?? new TodoDataModel()).Normalize();
        }
        catch
        {
            return new TodoDataModel().Normalize();
        }
    }

    public void Save(TodoDataModel data)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
        File.WriteAllText(filePath, JsonSerializer.Serialize(data.Normalize() with { UpdatedAt = DateTime.Now }, JsonOptions));
    }
}
