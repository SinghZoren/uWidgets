using System.Text.Json;
using Pomodoro.Models;
using Pomodoro.Services;
using ReactiveUI;
using uWidgets.Core.Interfaces;

namespace Pomodoro.ViewModels;

public class PomodoroSettingsViewModel(IWidgetLayoutProvider widgetLayoutProvider) : ReactiveObject
{
    private PomodoroModel model = (widgetLayoutProvider.Get().GetModel<PomodoroModel>() ?? new PomodoroModel()).Normalize();

    public int FocusMinutes
    {
        get => model.FocusMinutes;
        set => UpdateModel(model with { FocusMinutes = value }, PomodoroPhase.Focus);
    }

    public int ShortBreakMinutes
    {
        get => model.ShortBreakMinutes;
        set => UpdateModel(model with { ShortBreakMinutes = value }, PomodoroPhase.ShortBreak);
    }

    public int LongBreakMinutes
    {
        get => model.LongBreakMinutes;
        set => UpdateModel(model with { LongBreakMinutes = value }, PomodoroPhase.LongBreak);
    }

    public int SessionsBeforeLongBreak
    {
        get => model.SessionsBeforeLongBreak;
        set => UpdateModel(model with { SessionsBeforeLongBreak = value });
    }

    public string AlarmSoundPath
    {
        get => model.AlarmSoundPath;
        set => UpdateModel(model with { AlarmSoundPath = value });
    }

    public string AlarmSoundLabel => string.IsNullOrWhiteSpace(model.AlarmSoundPath)
        ? "No sound selected"
        : Path.GetFileName(model.AlarmSoundPath);

    public bool HasAlarmSound => !string.IsNullOrWhiteSpace(model.AlarmSoundPath);

    public void ClearAlarmSound() => AlarmSoundPath = string.Empty;

    public void TestAlarmSound() => PomodoroAlarmPlayer.Play(model.AlarmSoundPath);

    private void UpdateModel(PomodoroModel newModel, PomodoroPhase? changedPhase = null)
    {
        newModel = newModel.Normalize();

        if (!newModel.IsRunning && changedPhase == newModel.Phase)
            newModel = newModel with { RemainingSeconds = newModel.GetDurationSeconds() };

        model = newModel;
        var layout = widgetLayoutProvider.Get();
        widgetLayoutProvider.Save(layout with { Settings = JsonSerializer.SerializeToElement(model) });

        this.RaisePropertyChanged(nameof(FocusMinutes));
        this.RaisePropertyChanged(nameof(ShortBreakMinutes));
        this.RaisePropertyChanged(nameof(LongBreakMinutes));
        this.RaisePropertyChanged(nameof(SessionsBeforeLongBreak));
        this.RaisePropertyChanged(nameof(AlarmSoundPath));
        this.RaisePropertyChanged(nameof(AlarmSoundLabel));
        this.RaisePropertyChanged(nameof(HasAlarmSound));
    }
}
