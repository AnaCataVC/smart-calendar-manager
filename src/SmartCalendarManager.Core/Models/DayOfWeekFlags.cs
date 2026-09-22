using System;

namespace SmartCalendarManager.Core.Models;

/// <summary>
/// Bitmask flags representing days of the week.
/// </summary>
[Flags]
public enum DayOfWeekFlags
{
    None = 0,
    Monday = 1 << 0,    // 1
    Tuesday = 1 << 1,   // 2
    Wednesday = 1 << 2, // 4
    Thursday = 1 << 3,  // 8
    Friday = 1 << 4,    // 16
    Saturday = 1 << 5,  // 32
    Sunday = 1 << 6,    // 64

    Weekdays = Monday | Tuesday | Wednesday | Thursday | Friday,
    Weekend = Saturday | Sunday,
    All = Weekdays | Weekend
}

/// <summary>
/// Extension methods for DayOfWeekFlags.
/// </summary>
public static class DayOfWeekFlagsExtensions
{
    public static DayOfWeekFlags ToFlag(this DayOfWeek day)
    {
        return day switch
        {
            DayOfWeek.Monday => DayOfWeekFlags.Monday,
            DayOfWeek.Tuesday => DayOfWeekFlags.Tuesday,
            DayOfWeek.Wednesday => DayOfWeekFlags.Wednesday,
            DayOfWeek.Thursday => DayOfWeekFlags.Thursday,
            DayOfWeek.Friday => DayOfWeekFlags.Friday,
            DayOfWeek.Saturday => DayOfWeekFlags.Saturday,
            DayOfWeek.Sunday => DayOfWeekFlags.Sunday,
            _ => DayOfWeekFlags.None
        };
    }

    public static bool HasDay(this DayOfWeekFlags flags, DayOfWeek day)
    {
        return (flags & day.ToFlag()) != 0;
    }
}
