using System;
using System.Linq;

namespace SmartCalendarManager.Core.Models;

public enum GranolaScope
{
    WorkOnly,
    All
}

/// <summary>
/// Settings and evaluation policy for determining whether calendar events should trigger alerts / app launches.
/// </summary>
public class CalendarFilterSettings
{
    public const string DefaultExcludedKeywords = "[Personal], [Privado], Out of office, Fuera de la oficina, OOO, Vacaciones, Focus time";

    /// <summary>
    /// Gets or sets the comma-separated list of keywords or prefixes that disqualify an event from triggering actions.
    /// </summary>
    public string ExcludedKeywords { get; set; } = DefaultExcludedKeywords;

    /// <summary>
    /// Gets or sets whether all-day events (e.g. OOO, holidays, all-day focus blocks) should be ignored.
    /// </summary>
    public bool IgnoreAllDayEvents { get; set; } = true;

    /// <summary>
    /// Gets or sets whether actions should only open if the calendar event contains a valid video conference URL (Meet, Zoom, Teams, Webex).
    /// </summary>
    public bool RequireMeetingLink { get; set; }

    /// <summary>
    /// Gets or sets which calendar feeds Granola applies to (auto-open and the agenda button).
    /// </summary>
    public GranolaScope GranolaScope { get; set; } = GranolaScope.WorkOnly;

    public bool IsInGranolaScope(CalendarEvent calendarEvent) =>
        GranolaScope == GranolaScope.All || calendarEvent.FeedKind == CalendarFeedKind.Work;

    /// <summary>
    /// Evaluates a calendar event against the current filter rules to determine if Granola should be automatically opened.
    /// </summary>
    public bool ShouldOpenGranola(CalendarEvent? calendarEvent)
    {
        if (calendarEvent == null) return false;

        if (!IsInGranolaScope(calendarEvent))
        {
            return false;
        }

        // 1. Check all-day event rule
        if (IgnoreAllDayEvents && calendarEvent.IsAllDay)
        {
            return false;
        }

        // 2. Check video meeting link requirement
        if (RequireMeetingLink && string.IsNullOrWhiteSpace(calendarEvent.MeetingLink))
        {
            return false;
        }

        // 3. Check excluded keywords and prefixes
        if (!string.IsNullOrWhiteSpace(ExcludedKeywords) && !string.IsNullOrWhiteSpace(calendarEvent.Title))
        {
            var keywords = ExcludedKeywords
                .Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(k => k.Trim())
                .Where(k => !string.IsNullOrEmpty(k));

            foreach (var keyword in keywords)
            {
                if (calendarEvent.Title.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
            }
        }

        return true;
    }
}
