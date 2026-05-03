using Avalonia.Media;

namespace Notes.ViewModels;

public record NoteLineViewModel(
    int Index,
    string Raw,
    string Text,
    bool IsHeading,
    bool IsBullet,
    bool IsCheckbox,
    bool Completed,
    bool IsBold)
{
    public bool IsPlain => !IsBullet && !IsCheckbox;
    public string Bullet => IsBullet ? "-" : "";
    public FontWeight Weight => IsHeading || IsBold ? FontWeight.SemiBold : FontWeight.Normal;
    public IBrush TextBrush { get; init; } = Brushes.Black;
    public IBrush AccentBrush { get; init; } = Brushes.DodgerBlue;
    public FontFamily FontFamily { get; init; } = FontFamily.Default;
    public double FontSize { get; init; } = 14;
    public double LineHeight { get; init; } = 18;
    public TextAlignment TextAlignment { get; init; } = TextAlignment.Left;
    public bool LockEditing { get; init; }
    public bool CanEdit => !LockEditing;
}
