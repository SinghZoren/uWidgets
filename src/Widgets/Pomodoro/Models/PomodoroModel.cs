namespace Pomodoro.Models;

public record PomodoroModel(
    int FocusMinutes = 25,
    int ShortBreakMinutes = 5,
    int LongBreakMinutes = 15,
    int SessionsBeforeLongBreak = 4,
    PomodoroPhase Phase = PomodoroPhase.Focus,
    bool IsRunning = false,
    int RemainingSeconds = 25 * 60,
    int CompletedFocusSessions = 0,
    DateTime? LastStartedUtc = null,
    string AlarmSoundPath = "")
{
    public int GetDurationSeconds(PomodoroPhase? phase = null)
    {
        var minutes = (phase ?? Phase) switch
        {
            PomodoroPhase.ShortBreak => ShortBreakMinutes,
            PomodoroPhase.LongBreak => LongBreakMinutes,
            _ => FocusMinutes
        };

        return Math.Max(1, minutes) * 60;
    }

    public PomodoroModel Normalize()
    {
        var normalized = this with
        {
            FocusMinutes = Math.Clamp(FocusMinutes, 1, 180),
            ShortBreakMinutes = Math.Clamp(ShortBreakMinutes, 1, 60),
            LongBreakMinutes = Math.Clamp(LongBreakMinutes, 1, 120),
            SessionsBeforeLongBreak = Math.Clamp(SessionsBeforeLongBreak, 1, 12),
            CompletedFocusSessions = Math.Max(0, CompletedFocusSessions),
            AlarmSoundPath = AlarmSoundPath.Trim()
        };

        var duration = normalized.GetDurationSeconds();
        if (normalized.RemainingSeconds <= 0 || normalized.RemainingSeconds > duration)
            normalized = normalized with { RemainingSeconds = duration };

        return normalized;
    }
}
