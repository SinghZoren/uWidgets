using System.Runtime.InteropServices;
using System.Text;

namespace Pomodoro.Services;

internal static class PomodoroAlarmPlayer
{
    private static readonly object Gate = new();
    private static string? currentAlias;

    public static void Play(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path) || !OperatingSystem.IsWindows())
            return;

        lock (Gate)
        {
            StopNoThrow();

            var alias = "pomodoroAlarm" + Guid.NewGuid().ToString("N");
            var safePath = path.Replace("\"", string.Empty);

            if (Send($"open \"{safePath}\" type mpegvideo alias {alias}") != 0)
                return;

            currentAlias = alias;
            Send($"play {alias} from 0");

            _ = CloseLater(alias);
        }
    }

    public static void Stop()
    {
        lock (Gate)
            StopNoThrow();
    }

    private static async Task CloseLater(string alias)
    {
        await Task.Delay(TimeSpan.FromMinutes(5));

        lock (Gate)
        {
            if (currentAlias == alias)
                StopNoThrow();
        }
    }

    private static void StopNoThrow()
    {
        if (currentAlias == null)
            return;

        Send($"stop {currentAlias}");
        Send($"close {currentAlias}");
        currentAlias = null;
    }

    private static int Send(string command) => mciSendString(command, null, 0, IntPtr.Zero);

    [DllImport("winmm.dll", CharSet = CharSet.Unicode)]
    private static extern int mciSendString(string command, StringBuilder? returnValue, int returnLength, IntPtr callback);
}
