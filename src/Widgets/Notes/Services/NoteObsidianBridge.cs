using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Notes.Models;

namespace Notes.Services;

public static class NoteObsidianBridge
{
    public static bool IsConfigured(NoteModel settings) =>
        TryGetVaultPath(settings) != null;

    public static string? TryGetVaultPath(NoteModel settings)
    {
        if (!string.IsNullOrWhiteSpace(settings.ObsidianVaultPath) &&
            Directory.Exists(settings.ObsidianVaultPath))
            return Path.GetFullPath(settings.ObsidianVaultPath);

        return TryAutoDetectVaultPath();
    }

    public static IReadOnlyList<NoteDocumentModel> GetRecentNotes(NoteModel settings, int count = 12)
    {
        var vaultPath = TryGetVaultPath(settings);
        if (string.IsNullOrWhiteSpace(vaultPath))
            return [];

        try
        {
            return Directory.EnumerateFiles(vaultPath, "*.md", SearchOption.AllDirectories)
                .Where(path => !IsInsideObsidianSystemFolder(vaultPath, path))
                .Select(path => FromMarkdownFile(vaultPath, path))
                .OrderByDescending(note => note.UpdatedAt)
                .Take(count)
                .ToList();
        }
        catch
        {
            return [];
        }
    }

    public static NoteDocumentModel? CreateVaultNote(string? title, NoteModel settings)
    {
        var vaultPath = TryGetVaultPath(settings);
        if (string.IsNullOrWhiteSpace(vaultPath))
            return null;

        var now = DateTime.Now;
        var noteTitle = string.IsNullOrWhiteSpace(title) ? "Untitled note" : title.Trim();
        var fileName = UniqueMarkdownFile(vaultPath, SanitizeFileName(noteTitle));
        var content = $"# {noteTitle}{Environment.NewLine}{Environment.NewLine}";
        File.WriteAllText(fileName, content, Encoding.UTF8);
        return FromMarkdownFile(vaultPath, fileName) with
        {
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    public static string SyncDocument(NoteDocumentModel document, string vaultPath)
    {
        Directory.CreateDirectory(vaultPath);
        var relativePath = string.IsNullOrWhiteSpace(document.ObsidianPath)
            ? $"{SanitizeFileName(document.Title)}.md"
            : document.ObsidianPath!;

        var absolutePath = Path.Combine(vaultPath, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(absolutePath)!);
        File.WriteAllText(absolutePath, ToMarkdown(document), Encoding.UTF8);
        return relativePath;
    }

    public static void OpenDocument(NoteDocumentModel document, string vaultPath)
    {
        var relativePath = string.IsNullOrWhiteSpace(document.ObsidianPath)
            ? SyncDocument(document, vaultPath)
            : document.ObsidianPath!;
        var absolutePath = Path.GetFullPath(Path.Combine(vaultPath, relativePath));
        OpenFile(absolutePath);
    }

    public static void OpenDocument(NoteDocumentModel document, NoteModel settings)
    {
        var vaultPath = TryGetVaultPath(settings);
        if (string.IsNullOrWhiteSpace(vaultPath))
            return;

        OpenDocument(document, vaultPath);
    }

    public static void OpenFile(string absolutePath)
    {
        var uri = $"obsidian://open?path={Uri.EscapeDataString(absolutePath)}";

        try
        {
            Process.Start(new ProcessStartInfo(uri) { UseShellExecute = true });
        }
        catch
        {
            Process.Start(new ProcessStartInfo(absolutePath) { UseShellExecute = true });
        }
    }

    private static string ToMarkdown(NoteDocumentModel document)
    {
        var title = document.Title ?? "Untitled note";
        var content = document.Content ?? "";
        return content.TrimStart().StartsWith("# ", StringComparison.Ordinal)
            ? content
            : $"# {title}{Environment.NewLine}{Environment.NewLine}{content}";
    }

    private static string SanitizeFileName(string? value)
    {
        var name = string.IsNullOrWhiteSpace(value) ? "Untitled note" : value.Trim();
        foreach (var invalid in Path.GetInvalidFileNameChars())
            name = name.Replace(invalid, '-');

        name = string.Join(" ", name.Split(' ', StringSplitOptions.RemoveEmptyEntries));
        return string.IsNullOrWhiteSpace(name) ? "Untitled note" : name;
    }

    private static string? TryAutoDetectVaultPath()
    {
        try
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var configPath = Path.Combine(appData, "obsidian", "obsidian.json");
            if (!File.Exists(configPath))
                return null;

            using var doc = JsonDocument.Parse(File.ReadAllText(configPath));
            if (!doc.RootElement.TryGetProperty("vaults", out var vaults))
                return null;

            foreach (var vault in vaults.EnumerateObject()
                         .Select(item => item.Value)
                         .OrderByDescending(item => item.TryGetProperty("open", out var open) && open.GetBoolean())
                         .ThenByDescending(item => item.TryGetProperty("ts", out var ts) ? ts.GetInt64() : 0))
            {
                if (!vault.TryGetProperty("path", out var pathElement))
                    continue;

                var path = pathElement.GetString();
                if (!string.IsNullOrWhiteSpace(path) && Directory.Exists(path))
                    return Path.GetFullPath(path);
            }
        }
        catch
        {
            return null;
        }

        return null;
    }

    private static NoteDocumentModel FromMarkdownFile(string vaultPath, string absolutePath)
    {
        var relativePath = Path.GetRelativePath(vaultPath, absolutePath);
        var info = new FileInfo(absolutePath);
        return new NoteDocumentModel(
            Id: relativePath,
            Title: Path.GetFileNameWithoutExtension(absolutePath),
            Content: ReadPreviewContent(absolutePath),
            CreatedAt: info.CreationTime,
            UpdatedAt: info.LastWriteTime,
            ObsidianPath: relativePath);
    }

    private static bool IsInsideObsidianSystemFolder(string vaultPath, string path)
    {
        var relative = Path.GetRelativePath(vaultPath, path);
        return relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            .Any(part => part.Equals(".obsidian", StringComparison.OrdinalIgnoreCase) ||
                         part.Equals(".trash", StringComparison.OrdinalIgnoreCase));
    }

    private static string UniqueMarkdownFile(string vaultPath, string stem)
    {
        Directory.CreateDirectory(vaultPath);
        var candidate = Path.Combine(vaultPath, $"{stem}.md");
        if (!File.Exists(candidate))
            return candidate;

        for (var index = 2; index < 1000; index++)
        {
            candidate = Path.Combine(vaultPath, $"{stem} {index}.md");
            if (!File.Exists(candidate))
                return candidate;
        }

        return Path.Combine(vaultPath, $"{stem} {DateTime.Now:yyyyMMddHHmmss}.md");
    }

    private static string ReadPreviewContent(string path)
    {
        try
        {
            using var stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var reader = new StreamReader(stream, Encoding.UTF8, true);
            var buffer = new char[4096];
            var read = reader.Read(buffer, 0, buffer.Length);
            return new string(buffer, 0, read);
        }
        catch
        {
            return "";
        }
    }
}
