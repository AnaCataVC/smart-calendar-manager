using System;

namespace SmartCalendarManager.Core.Models;

/// <summary>
/// Type of target action to execute when a cron schedule fires.
/// </summary>
public enum CronActionType
{
    Executable,
    UriProtocol,
    WebUrl
}

/// <summary>
/// Represents a user-configured scheduled app or link launch rule.
/// </summary>
public class CronLauncherRule
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = "Nueva regla";
    public bool IsEnabled { get; set; } = true;
    public DayOfWeekFlags Days { get; set; } = DayOfWeekFlags.Weekdays;
    public TimeSpan TimeOfDay { get; set; } = new TimeSpan(9, 0, 0);
    public CronActionType ActionType { get; set; } = CronActionType.UriProtocol;
    public string Target { get; set; } = "slack://";
    public string? Arguments { get; set; }

    public string FormattedTime => DateTime.Today.Add(TimeOfDay).ToString("h:mm tt");
    public string FormattedDays
    {
        get
        {
            if (Days == DayOfWeekFlags.All) return "Todos los días";
            if (Days == DayOfWeekFlags.Weekdays) return "Lun - Vie";
            if (Days == DayOfWeekFlags.Weekend) return "Sáb - Dom";
            if (Days == DayOfWeekFlags.None) return "Ninguno";

            var dayNames = new System.Collections.Generic.List<string>();
            if (Days.HasFlag(DayOfWeekFlags.Monday)) dayNames.Add("Lun");
            if (Days.HasFlag(DayOfWeekFlags.Tuesday)) dayNames.Add("Mar");
            if (Days.HasFlag(DayOfWeekFlags.Wednesday)) dayNames.Add("Mié");
            if (Days.HasFlag(DayOfWeekFlags.Thursday)) dayNames.Add("Jue");
            if (Days.HasFlag(DayOfWeekFlags.Friday)) dayNames.Add("Vie");
            if (Days.HasFlag(DayOfWeekFlags.Saturday)) dayNames.Add("Sáb");
            if (Days.HasFlag(DayOfWeekFlags.Sunday)) dayNames.Add("Dom");

            return string.Join(", ", dayNames);
        }
    }
}
