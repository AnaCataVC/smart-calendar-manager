using System.Collections.Generic;

namespace SmartCalendarManager.Core.Services;

/// <summary>
/// Abstraction for operating system process inspection and termination.
/// </summary>
public interface IProcessController
{
    /// <summary>
    /// Gets the names of all currently running processes.
    /// </summary>
    IReadOnlyList<string> GetRunningProcessNames();

    /// <summary>
    /// Checks if at least one process with the given name is currently running.
    /// </summary>
    bool IsProcessRunning(string processName);

    /// <summary>
    /// Attempts to terminate all processes matching the specified process name.
    /// Attempts graceful closure (CloseMainWindow) first, falling back to Kill.
    /// Returns the number of processes successfully terminated.
    /// </summary>
    int TerminateProcessesByName(string processName, int gracefulTimeoutMs = 1500);
}
