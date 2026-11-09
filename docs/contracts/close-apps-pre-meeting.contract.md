# Interface & Behavioral Contract: Pre-Meeting App Closer Service

**Feature Slug:** `close-apps-pre-meeting`  
**Status:** Frozen  
**Target Projects:** `SmartCalendarManager.Core`, `SmartCalendarManager.App`, `SmartCalendarManager.Tests`  

---

## 1. Scope & Objective

Provide automated and manual termination of distracting or sound-emitting background/foreground applications (such as Spotify, Discord, or Steam) prior to the start of scheduled calendar meetings (by default 1 minute before start time), with Just-In-Time (JIT) calendar verification to prevent closing apps for moved or cancelled events.

---

## 2. Public Interfaces & Signatures

Namespace: `SmartCalendarManager.Core.Services`

### 2.1. `IProcessController`
Low-level operating system process interaction layer. Decouples production code from `System.Diagnostics.Process` to guarantee 100% in-memory testability in xUnit.

```csharp
namespace SmartCalendarManager.Core.Services;

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
    /// First attempts graceful closure (CloseMainWindow) within the specified timeout,
    /// followed by forced termination (Kill) for background or unresponsive processes.
    /// Returns the number of processes successfully terminated.
    /// </summary>
    int TerminateProcessesByName(string processName, int gracefulTimeoutMs = 1500);
}
```

### 2.2. `IAppCloserService`
High-level domain service for parsing configured process lists, detecting active targets, and executing closures.

```csharp
namespace SmartCalendarManager.Core.Services;

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
```

---

## 3. Invariants & Behavioral Rules

1. **Process Name Sanitization:**
   - Case-insensitive matching.
   - Automatic stripping of `.exe` suffix if provided (e.g., `Spotify.exe` -> `Spotify`).
   - Trimming whitespace and discarding empty or null entries.
2. **Termination Safety:**
   - For GUI processes with a valid window handle, `CloseMainWindow()` must be attempted first.
   - For background/minimized processes (e.g. Spotify minimized to tray with `MainWindowHandle == IntPtr.Zero`) or processes that do not exit within `gracefulTimeoutMs`, `Kill()` is invoked to guarantee audio playback ceases.
3. **Just-In-Time (JIT) Verification:**
   - The pre-meeting timer callback must perform a live verification against the iCal feed before executing `CloseConfiguredApps`.
   - If the event is cancelled, moved, or declined, app closure is aborted.
