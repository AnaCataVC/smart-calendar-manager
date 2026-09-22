using System;
using System.Collections.Generic;

namespace SmartCalendarManager.Core.Models;

/// <summary>
/// Persistent mapping record for personal -> work blocked event reconciliation.
/// </summary>
public class SyncMappingRecord
{
    public string PersonalEventId { get; set; } = string.Empty;
    public string WorkCalendarEventId { get; set; } = string.Empty;
    public DateTime LastSynchronizedUtc { get; set; } = DateTime.UtcNow;
    public DateTime PersonalStart { get; set; }
    public DateTime PersonalEnd { get; set; }
}

/// <summary>
/// Container for OAuth calendar synchronization settings and tokens.
/// </summary>
public class CalendarSyncSettings
{
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public string PersonalCalendarId { get; set; } = "primary";
    public string WorkCalendarId { get; set; } = string.Empty;
    public string BlockEventTitle { get; set; } = "🔒 Ocupada";
    public string LastSyncToken { get; set; } = string.Empty;
    public DateTime? LastSyncTime { get; set; }
    public bool AutoSyncEnabled { get; set; } = true;
    public int SyncIntervalMinutes { get; set; } = 15;
    public Dictionary<string, SyncMappingRecord> EventMappings { get; set; } = new();
}
