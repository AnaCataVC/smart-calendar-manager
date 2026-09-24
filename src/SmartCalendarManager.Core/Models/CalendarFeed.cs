using System;
using System.Text.Json.Serialization;

namespace SmartCalendarManager.Core.Models;

public enum CalendarFeedKind
{
    Work,
    Personal
}

/// <summary>
/// A secret iCal (.ics) URL the app reads events from.
/// </summary>
public class CalendarFeed
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public CalendarFeedKind Kind { get; set; } = CalendarFeedKind.Work;
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Optional HTTP Basic password for feeds served behind authentication; null for secret URLs.
    /// </summary>
    public string? AuthKey { get; set; }

    [JsonIgnore]
    public string KindLabel => LabelFor(Kind);

    public static string LabelFor(CalendarFeedKind kind) => kind == CalendarFeedKind.Work ? "Laboral" : "Personal";
}
