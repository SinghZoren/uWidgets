using System.Text.Json;
using Avalonia.Controls;
using Notes.Locales;
using Notes.Models;
using uWidgets.Core.Interfaces;
using uWidgets.Core.Models;

namespace Notes.ViewModels;

public class NoteSettingsViewModel
{
    private readonly IWidgetLayoutProvider widgetLayoutProvider;
    private NoteModel model;

    public NoteSettingsViewModel(IWidgetLayoutProvider widgetLayoutProvider)
    {
        this.widgetLayoutProvider = widgetLayoutProvider;
        model = GetModel();
    }

    public bool IsPinned
    {
        get => model.IsPinned;
        set => Update(model with { IsPinned = value });
    }

    public bool IsChecklistMode
    {
        get => model.IsChecklistMode;
        set => Update(model with { IsChecklistMode = value });
    }

    public bool IsCompact
    {
        get => model.IsCompact;
        set => Update(model with { IsCompact = value });
    }

    public bool LockEditing
    {
        get => model.LockEditing;
        set => Update(model with { LockEditing = value });
    }

    public bool AlwaysOnTop
    {
        get => model.AlwaysOnTop;
        set => Update(model with { AlwaysOnTop = value });
    }

    public bool UseBlur
    {
        get => model.UseBlur;
        set => Update(model with { UseBlur = value });
    }

    public bool LocalBackup
    {
        get => model.LocalBackup;
        set => Update(model with { LocalBackup = value });
    }

    public bool SyncWithObsidian
    {
        get => model.SyncWithObsidian;
        set => Update(model with { SyncWithObsidian = value });
    }

    public string? ObsidianVaultPath
    {
        get => model.ObsidianVaultPath;
        set => Update(model with { ObsidianVaultPath = value });
    }

    public double OpacityLevel
    {
        get => model.OpacityLevel;
        set => Update(model with { OpacityLevel = value });
    }

    public string? BackgroundColor
    {
        get => model.BackgroundColor;
        set => Update(model with { BackgroundColor = value });
    }

    public string? HeaderColor
    {
        get => model.HeaderColor;
        set => Update(model with { HeaderColor = value });
    }

    public string? TextColor
    {
        get => model.TextColor;
        set => Update(model with { TextColor = value });
    }

    public string? AccentColor
    {
        get => model.AccentColor;
        set => Update(model with { AccentColor = value });
    }

    public string? FontFamily
    {
        get => model.FontFamily;
        set => Update(model with { FontFamily = value });
    }

    public double FontSize
    {
        get => model.FontSize;
        set => Update(model with { FontSize = value });
    }

    public double LineSpacing
    {
        get => model.LineSpacing;
        set => Update(model with { LineSpacing = value });
    }

    public NoteTextAlignment TextAlignment
    {
        get => model.TextAlignment;
        set => Update(model with { TextAlignment = value });
    }

    public int MaxCharacters
    {
        get => model.MaxCharacters;
        set => Update(model with { MaxCharacters = value });
    }

    public int MaxLines
    {
        get => model.MaxLines;
        set => Update(model with { MaxLines = value });
    }

    public string[] Fonts => ["Inter", "Segoe UI", "Microsoft YaHei", "Consolas", "Georgia"];
    public NoteTextAlignment[] Alignments => [NoteTextAlignment.Left, NoteTextAlignment.Center, NoteTextAlignment.Right];
    public NoteTemplateViewModel[] Templates =>
    [
        new(Locale.Notes_Template_ToDoList, NoteTemplate.ToDoList),
        new(Locale.Notes_Template_DailyPlan, NoteTemplate.DailyPlan),
        new(Locale.Notes_Template_Scratchpad, NoteTemplate.Scratchpad),
        new(Locale.Notes_Template_MeetingNotes, NoteTemplate.MeetingNotes),
        new(Locale.Notes_Template_ClassNotes, NoteTemplate.ClassNotes)
    ];

    public NoteTemplateViewModel? SelectedTemplate
    {
        get => null;
        set
        {
            if (value == null) return;
            Update(ApplyTemplate(value.Value));
        }
    }

    public void Clear() => Update(model with { Content = "", Updated = DateTime.Now });

    public WidgetLayout CreateDuplicate(bool includeContent)
    {
        var current = widgetLayoutProvider.Get();
        var duplicate = model with
        {
            Id = Guid.NewGuid().ToString("N"),
            Title = includeContent ? $"{model.Title} Copy" : model.Title,
            Content = includeContent ? model.Content : "",
            Updated = DateTime.Now
        };

        return current with
        {
            X = current.X + 32,
            Y = current.Y + 32,
            Settings = JsonSerializer.SerializeToElement(duplicate.Normalize())
        };
    }

    private NoteModel GetModel() =>
        (widgetLayoutProvider.Get().GetModel<NoteModel>() ?? new NoteModel(Locale.Notes_Title)).Normalize();

    private void Update(NoteModel newModel)
    {
        model = EnforceLimits(newModel.Normalize());
        var layout = widgetLayoutProvider.Get();
        widgetLayoutProvider.Save(layout with { Settings = JsonSerializer.SerializeToElement(model) });
    }

    private static NoteModel EnforceLimits(NoteModel model)
    {
        var content = model.Content ?? "";

        if (model.MaxLines > 0)
            content = string.Join(Environment.NewLine, content.Replace("\r\n", "\n").Split('\n').Take(model.MaxLines));

        if (model.MaxCharacters > 0 && content.Length > model.MaxCharacters)
            content = content[..model.MaxCharacters];

        return model with { Content = content };
    }

    private NoteModel ApplyTemplate(NoteTemplate template)
    {
        return template switch
        {
            NoteTemplate.ToDoList => model with
            {
                Title = Locale.Notes_Template_ToDoList,
                Content = "- [ ] First task\n- [ ] Next task\n- [ ] Done",
                IsChecklistMode = true,
                Updated = DateTime.Now
            },
            NoteTemplate.DailyPlan => model with
            {
                Title = Locale.Notes_Template_DailyPlan,
                Content = "# Today\n- [ ] Top priority\n- [ ] Follow up\n\n# Notes\n",
                IsChecklistMode = false,
                Updated = DateTime.Now
            },
            NoteTemplate.MeetingNotes => model with
            {
                Title = Locale.Notes_Template_MeetingNotes,
                Content = "# Meeting\n**Attendees:**\n\n**Decisions:**\n- \n\n**Action items:**\n- [ ] ",
                IsChecklistMode = false,
                Updated = DateTime.Now
            },
            NoteTemplate.ClassNotes => model with
            {
                Title = Locale.Notes_Template_ClassNotes,
                Content = "# Topic\n**Key ideas:**\n- \n\n**Questions:**\n- \n\n**Review:**\n- [ ] ",
                IsChecklistMode = false,
                Updated = DateTime.Now
            },
            _ => model with
            {
                Title = Locale.Notes_Template_Scratchpad,
                Content = "",
                IsChecklistMode = false,
                Updated = DateTime.Now
            }
        };
    }
}
