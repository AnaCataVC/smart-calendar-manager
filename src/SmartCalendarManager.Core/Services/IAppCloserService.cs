using System.Collections.Generic;

namespace SmartCalendarManager.Core.Services;

/// <summary>
/// Domain service for parsing process lists and terminating configured applications before meetings.
/// </summary>
public interface IAppCloserService
{
    /// <summary>
    /// Parses a delimiter-separated string of process names (comma, semicolon, or newline)
    /// into clean, distinct process names without extensions or invalid characters.
    /// </summary>
    IReadOnlyList<string> ParseProcessNames(string? processListString);

    /// <summary>
    /// Closes all active processes listed in the configured process string.
    /// Returns the total count of terminated processes.
    /// </summary>
    int CloseConfiguredApps(string? processListString);

    /// <summary>
    /// Returns which of the configured apps are currently running.
    /// </summary>
    IReadOnlyList<string> GetRunningConfiguredApps(string? processListString);
}
