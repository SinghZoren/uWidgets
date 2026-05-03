using System.Globalization;
using System.Text.RegularExpressions;
using Reminders.Models;

namespace Reminders.Services;

public static class TodoParser
{
    public static TodoTaskDraft Parse(string input)
    {
        var raw = input.Trim();
        var text = raw;
        var category = "Uncategorized";
        var priority = TodoPriority.None;
        var recurrence = TodoRecurrence.None;
        string? notes = null;

        var categoryMatch = Regex.Match(text, @"#([A-Za-z0-9_-]+)");
        if (categoryMatch.Success)
        {
            category = categoryMatch.Groups[1].Value.Trim().ToLowerInvariant();
            text = Regex.Replace(text, @"\s*#[A-Za-z0-9_-]+", "", RegexOptions.IgnoreCase);
        }

        var priorityMatch = Regex.Match(text, @"!(high|medium|low|none|urgent|p1|p2|p3)\b", RegexOptions.IgnoreCase);
        if (priorityMatch.Success)
        {
            priority = priorityMatch.Groups[1].Value.ToLowerInvariant() switch
            {
                "high" or "urgent" or "p1" => TodoPriority.High,
                "medium" or "p2" => TodoPriority.Medium,
                "low" or "p3" => TodoPriority.Low,
                _ => TodoPriority.None
            };
            text = Regex.Replace(text, @"\s*!(high|medium|low|none|urgent|p1|p2|p3)\b", "", RegexOptions.IgnoreCase);
        }

        recurrence = ExtractRecurrence(ref text);
        var dueTime = ExtractTime(ref text);
        var dueDate = ExtractDate(ref text);

        if (text.Contains(" -- ", StringComparison.Ordinal))
        {
            var parts = text.Split(" -- ", 2, StringSplitOptions.TrimEntries);
            text = parts[0];
            notes = parts[1];
        }

        text = Regex.Replace(text, @"\b(due|at)\b", "", RegexOptions.IgnoreCase);
        text = Regex.Replace(text, @"\s+", " ").Trim();

        return new TodoTaskDraft(
            string.IsNullOrWhiteSpace(text) ? raw : text,
            dueDate,
            dueTime,
            priority,
            category,
            recurrence,
            notes);
    }

    public static DateTime? ParseDate(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return null;

        var text = input.Trim();
        return ExtractDate(ref text) ?? (DateTime.TryParse(input, out var parsed) ? parsed.Date : null);
    }

    public static TimeSpan? ParseTime(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return null;

        var text = input.Trim();
        return ExtractTime(ref text) ?? (TimeSpan.TryParse(input, out var parsed) ? parsed : null);
    }

    private static TodoRecurrence ExtractRecurrence(ref string text)
    {
        if (Regex.IsMatch(text, @"\b(every day|daily|everyday)\b", RegexOptions.IgnoreCase))
        {
            text = Regex.Replace(text, @"\b(every day|daily|everyday)\b", "", RegexOptions.IgnoreCase);
            return TodoRecurrence.Daily;
        }

        if (Regex.IsMatch(text, @"\b(every week|weekly)\b", RegexOptions.IgnoreCase))
        {
            text = Regex.Replace(text, @"\b(every week|weekly)\b", "", RegexOptions.IgnoreCase);
            return TodoRecurrence.Weekly;
        }

        if (Regex.IsMatch(text, @"\b(every month|monthly)\b", RegexOptions.IgnoreCase))
        {
            text = Regex.Replace(text, @"\b(every month|monthly)\b", "", RegexOptions.IgnoreCase);
            return TodoRecurrence.Monthly;
        }

        return TodoRecurrence.None;
    }

    private static TimeSpan? ExtractTime(ref string text)
    {
        var match = Regex.Match(text, @"\b(1[0-2]|0?[1-9])(?::([0-5][0-9]))?\s*(am|pm)\b", RegexOptions.IgnoreCase);
        if (match.Success)
        {
            var hour = int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
            var minute = match.Groups[2].Success ? int.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture) : 0;
            var period = match.Groups[3].Value.ToLowerInvariant();
            if (period == "pm" && hour < 12)
                hour += 12;
            if (period == "am" && hour == 12)
                hour = 0;

            text = text.Replace(match.Value, "", StringComparison.OrdinalIgnoreCase);
            return new TimeSpan(hour, minute, 0);
        }

        match = Regex.Match(text, @"\b([01]?[0-9]|2[0-3]):([0-5][0-9])\b");
        if (!match.Success)
            return null;

        text = text.Replace(match.Value, "", StringComparison.OrdinalIgnoreCase);
        return new TimeSpan(
            int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture),
            int.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture),
            0);
    }

    private static DateTime? ExtractDate(ref string text)
    {
        var today = DateTime.Today;
        if (Regex.IsMatch(text, @"\btoday\b", RegexOptions.IgnoreCase))
        {
            text = Regex.Replace(text, @"\btoday\b", "", RegexOptions.IgnoreCase);
            return today;
        }

        if (Regex.IsMatch(text, @"\b(tomorrow|tmrw)\b", RegexOptions.IgnoreCase))
        {
            text = Regex.Replace(text, @"\b(tomorrow|tmrw)\b", "", RegexOptions.IgnoreCase);
            return today.AddDays(1);
        }

        if (Regex.IsMatch(text, @"\bnext week\b", RegexOptions.IgnoreCase))
        {
            text = Regex.Replace(text, @"\bnext week\b", "", RegexOptions.IgnoreCase);
            return today.AddDays(7);
        }

        var weekday = Regex.Match(text, @"\b(next\s+)?(monday|tuesday|wednesday|thursday|friday|saturday|sunday)\b", RegexOptions.IgnoreCase);
        if (weekday.Success)
        {
            text = text.Replace(weekday.Value, "", StringComparison.OrdinalIgnoreCase);
            var target = Enum.Parse<DayOfWeek>(TitleCase(weekday.Groups[2].Value));
            var days = ((int)target - (int)today.DayOfWeek + 7) % 7;
            if (days == 0 || weekday.Groups[1].Success)
                days += 7;
            return today.AddDays(days);
        }

        var dateMatch = Regex.Match(text, @"\b\d{1,2}[/-]\d{1,2}([/-]\d{2,4})?\b");
        if (dateMatch.Success && DateTime.TryParse(dateMatch.Value, out var parsed))
        {
            text = text.Replace(dateMatch.Value, "", StringComparison.OrdinalIgnoreCase);
            return parsed.Date;
        }

        return null;
    }

    private static string TitleCase(string value) =>
        CultureInfo.CurrentCulture.TextInfo.ToTitleCase(value.Trim().ToLowerInvariant());
}
