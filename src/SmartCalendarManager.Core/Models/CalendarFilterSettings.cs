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
    public const string DefaultAppsToClose = "Spotify";

    /// <summary>
    /// Gets or sets whether applications should be automatically closed before eligible meetings.
    /// </summary>
    public bool CloseAppsBeforeMeetingEnabled { get; set; } = true;

    /// <summary>
    /// Gets or sets the delimiter-separated list of process names to close before meetings (e.g. "Spotify, Discord").
    /// </summary>
    public string AppsToClose { get; set; } = DefaultAppsToClose;

    /// <summary>
    /// Gets or sets how many minutes before an eligible meeting the configured apps should be closed.
    /// </summary>
    public int CloseAppsLeadMinutes { get; set; } = 1;

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
    public bool RequireMeetingLink { get; set; } = true;

    /// <summary>
    /// Gets or sets how many minutes before an eligible meeting Granola is opened.
    /// </summary>
    public int GranolaLeadMinutes { get; set; } = 5;

    /// <summary>
    /// Gets or sets which calendar feeds Granola applies to (auto-open and the agenda button).
    /// </summary>
    public GranolaScope GranolaScope { get; set; } = GranolaScope.WorkOnly;

    public bool IsInGranolaScope(CalendarEvent calendarEvent) =>
        GranolaScope == GranolaScope.All || calendarEvent.FeedKind == CalendarFeedKind.Work;

    /// <summary>
    /// Evaluates a calendar event against filter rules to determine if configured apps should be closed.
    /// </summary>
    public bool ShouldCloseApps(CalendarEvent? calendarEvent)
    {
        if (!CloseAppsBeforeMeetingEnabled || string.IsNullOrWhiteSpace(AppsToClose))
        {
            return false;
        }

        return ShouldOpenGranola(calendarEvent);
    }

    /// <summary>
    /// Evaluates a calendar event against the current filter rules to determine if Granola should be automatically opened.
    /// </summary>
    public bool ShouldOpenGranola(CalendarEvent? calendarEvent)
    {
        if (calendarEvent == null || calendarEvent.IsDeclined) return false;

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
