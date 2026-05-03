using System.Text.Json;
using Avalonia.Media;
using Pomodoro.Locales;
using Pomodoro.Models;
using Pomodoro.Services;
using ReactiveUI;
using uWidgets.Core.Interfaces;
using uWidgets.Services;

namespace Pomodoro.ViewModels;

public enum PomodoroWidgetViewMode
{
    Small,
    Medium,
    Large,
    ExtraLarge
}

public class PomodoroViewModel : ReactiveObject, IDisposable
{
    private readonly IWidgetLayoutProvider widgetLayoutProvider;
    private readonly UpdateTimer timer = TimerService.Timer1Second;
    private PomodoroModel model;
    private PomodoroWidgetViewMode viewMode = PomodoroWidgetViewMode.Small;

    public PomodoroViewModel(PomodoroModel model, IWidgetLayoutProvider widgetLayoutProvider)
    {
        this.widgetLayoutProvider = widgetLayoutProvider;
        this.model = model.Normalize();
        timer.Subscribe(OnTimerTick);
    }

    public string PhaseText => model.Phase switch
    {
        PomodoroPhase.ShortBreak => Locale.Pomodoro_ShortBreak,
        PomodoroPhase.LongBreak => Locale.Pomodoro_LongBreak,
        _ => Locale.Pomodoro_Focus
    };

    public string TimeText
    {
        get
        {
            var remaining = CurrentRemainingSeconds;
            return $"{remaining / 60:00}:{remaining % 60:00}";
        }
    }

    public double ProgressPercent
    {
        get
        {
            var duration = Math.Max(1, model.GetDurationSeconds());
            var elapsed = duration - CurrentRemainingSeconds;
            return Math.Clamp(elapsed * 100.0 / duration, 0, 100);
        }
    }

    public string StartPauseText => model.IsRunning ? Locale.Pomodoro_Pause : Locale.Pomodoro_Start;
    public string RunningText => model.IsRunning ? "Running" : "Ready";
    public string PhaseInitial => PhaseText[..1];

    public string CompletedText => string.Format(
        Locale.Pomodoro_Completed,
        model.CompletedFocusSessions,
        model.SessionsBeforeLongBreak);

    public bool IsRunning => model.IsRunning;
    public bool IsSmallLayout => viewMode == PomodoroWidgetViewMode.Small;
    public bool IsMediumLayout => viewMode == PomodoroWidgetViewMode.Medium;
    public bool IsLargeLayout => viewMode == PomodoroWidgetViewMode.Large;
    public bool IsExtraLargeLayout => viewMode == PomodoroWidgetViewMode.ExtraLarge;
    public string FocusDurationText => $"{model.FocusMinutes}m focus";
    public string ShortBreakDurationText => $"{model.ShortBreakMinutes}m short";
    public string LongBreakDurationText => $"{model.LongBreakMinutes}m long";
    public string NextPhaseText => model.Phase == PomodoroPhase.Focus
        ? model.CompletedFocusSessions + 1 >= model.SessionsBeforeLongBreak ? Locale.Pomodoro_LongBreak : Locale.Pomodoro_ShortBreak
        : Locale.Pomodoro_Focus;
    public string CycleText => $"{model.CompletedFocusSessions}/{model.SessionsBeforeLongBreak}";
    public IBrush WindowBackgroundBrush => Brush("#CC101116");
    public IBrush GlassBrush => Brush("#CC12151B");
    public IBrush PanelBrush => Brush("#12FFFFFF");
    public IBrush StrongPanelBrush => Brush("#1AFFFFFF");
    public IBrush BorderBrush => Brush("#26FFFFFF");
    public IBrush TrackBrush => Brush("#18FFFFFF");
    public IBrush TextBrush => Brush("#F6F7FB");
    public IBrush MutedBrush => Brush("#A5AEB9");
    public IBrush PhaseBrush => Brush(model.Phase switch
    {
        PomodoroPhase.ShortBreak => "#86A8FF",
        PomodoroPhase.LongBreak => "#D6A0FF",
        _ => "#7BE0C6"
    });
    public IBrush PhaseSoftBrush => Brush(model.Phase switch
    {
        PomodoroPhase.ShortBreak => "#2686A8FF",
        PomodoroPhase.LongBreak => "#26D6A0FF",
        _ => "#267BE0C6"
    });

    public void StartPause()
    {
        if (model.IsRunning)
        {
            SaveModel(model with
            {
                IsRunning = false,
                RemainingSeconds = CurrentRemainingSeconds,
                LastStartedUtc = null
            });
            return;
        }

        var remaining = CurrentRemainingSeconds;
        SaveModel(model with
        {
            IsRunning = true,
            RemainingSeconds = remaining > 0 ? remaining : model.GetDurationSeconds(),
            LastStartedUtc = DateTime.UtcNow
        });
    }

    public void Reset()
    {
        SaveModel(model with
        {
            IsRunning = false,
            RemainingSeconds = model.GetDurationSeconds(),
            LastStartedUtc = null
        });
    }

    public void Skip() => AdvancePhase(false);

    public void Dispose()
    {
        timer.Unsubscribe(OnTimerTick);
        GC.SuppressFinalize(this);
    }

    public void SetViewport(double width, double height)
    {
        var nextMode = (width, height) switch
        {
            ({ } w, { } h) when w >= 500 && h >= 220 => PomodoroWidgetViewMode.ExtraLarge,
            ({ } w, { } h) when w >= 220 && h >= 220 => PomodoroWidgetViewMode.Large,
            ({ } w, { } h) when w >= 220 => PomodoroWidgetViewMode.Medium,
            _ => PomodoroWidgetViewMode.Small
        };

        if (nextMode == viewMode)
            return;

        viewMode = nextMode;
        RaiseTimerProperties();
    }

    private int CurrentRemainingSeconds
    {
        get
        {
            if (!model.IsRunning || model.LastStartedUtc == null)
                return Math.Max(0, model.RemainingSeconds);

            var elapsed = (int)(DateTime.UtcNow - model.LastStartedUtc.Value).TotalSeconds;
            return Math.Max(0, model.RemainingSeconds - elapsed);
        }
    }

    private void OnTimerTick()
    {
        if (!model.IsRunning) return;

        if (CurrentRemainingSeconds <= 0)
        {
            AdvancePhase(true);
            return;
        }

        RaiseTimerProperties();
    }

    private void AdvancePhase(bool countCompletedFocus)
    {
        if (countCompletedFocus)
            PomodoroAlarmPlayer.Play(model.AlarmSoundPath);

        var completedFocusSessions = model.CompletedFocusSessions;
        if (countCompletedFocus && model.Phase == PomodoroPhase.Focus)
            completedFocusSessions++;

        var nextPhase = GetNextPhase(model.Phase, completedFocusSessions);
        var nextModel = model with
        {
            Phase = nextPhase,
            IsRunning = false,
            CompletedFocusSessions = completedFocusSessions,
            LastStartedUtc = null
        };

        SaveModel(nextModel with { RemainingSeconds = nextModel.GetDurationSeconds(nextPhase) });
    }

    private PomodoroPhase GetNextPhase(PomodoroPhase phase, int completedFocusSessions)
    {
        if (phase != PomodoroPhase.Focus)
            return PomodoroPhase.Focus;

        return completedFocusSessions > 0 &&
               completedFocusSessions % model.SessionsBeforeLongBreak == 0
            ? PomodoroPhase.LongBreak
            : PomodoroPhase.ShortBreak;
    }

    private void SaveModel(PomodoroModel newModel)
    {
        model = newModel.Normalize();
        var layout = widgetLayoutProvider.Get();
        if (layout != null)
            widgetLayoutProvider.Save(layout with { Settings = JsonSerializer.SerializeToElement(model) });

        RaiseTimerProperties();
    }

    private void RaiseTimerProperties()
    {
        this.RaisePropertyChanged(nameof(PhaseText));
        this.RaisePropertyChanged(nameof(TimeText));
        this.RaisePropertyChanged(nameof(ProgressPercent));
        this.RaisePropertyChanged(nameof(StartPauseText));
        this.RaisePropertyChanged(nameof(RunningText));
        this.RaisePropertyChanged(nameof(PhaseInitial));
        this.RaisePropertyChanged(nameof(CompletedText));
        this.RaisePropertyChanged(nameof(IsRunning));
        this.RaisePropertyChanged(nameof(IsSmallLayout));
        this.RaisePropertyChanged(nameof(IsMediumLayout));
        this.RaisePropertyChanged(nameof(IsLargeLayout));
        this.RaisePropertyChanged(nameof(IsExtraLargeLayout));
        this.RaisePropertyChanged(nameof(FocusDurationText));
        this.RaisePropertyChanged(nameof(ShortBreakDurationText));
        this.RaisePropertyChanged(nameof(LongBreakDurationText));
        this.RaisePropertyChanged(nameof(NextPhaseText));
        this.RaisePropertyChanged(nameof(CycleText));
        this.RaisePropertyChanged(nameof(WindowBackgroundBrush));
        this.RaisePropertyChanged(nameof(GlassBrush));
        this.RaisePropertyChanged(nameof(PanelBrush));
        this.RaisePropertyChanged(nameof(StrongPanelBrush));
        this.RaisePropertyChanged(nameof(BorderBrush));
        this.RaisePropertyChanged(nameof(TrackBrush));
        this.RaisePropertyChanged(nameof(TextBrush));
        this.RaisePropertyChanged(nameof(MutedBrush));
        this.RaisePropertyChanged(nameof(PhaseBrush));
        this.RaisePropertyChanged(nameof(PhaseSoftBrush));
    }

    private static IBrush Brush(string color) => SolidColorBrush.Parse(color);
}
