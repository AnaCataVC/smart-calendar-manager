using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Microsoft.Extensions.Logging;

namespace SmartCalendarManager.Core.Services;

/// <summary>
/// Production implementation of <see cref="IProcessController"/> using .NET Process APIs.
/// </summary>
public class WindowsProcessController : IProcessController
{
    private readonly ILogger<WindowsProcessController> _logger;

    public WindowsProcessController(ILogger<WindowsProcessController> logger)
    {
        _logger = logger;
    }

    public IReadOnlyList<string> GetRunningProcessNames()
    {
        try
        {
            return Process.GetProcesses()
                .Select(p => p.ProcessName)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(n => n)
                .ToList();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to enumerate running processes.");
            return Array.Empty<string>();
        }
    }

    public bool IsProcessRunning(string processName)
    {
        if (string.IsNullOrWhiteSpace(processName)) return false;

        try
        {
            var cleanName = SanitizeProcessName(processName);
            var processes = Process.GetProcessesByName(cleanName);
            try
            {
                return processes.Length > 0;
            }
            finally
            {
                foreach (var p in processes) p.Dispose();
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to check if process '{ProcessName}' is running.", processName);
            return false;
        }
    }

    public int TerminateProcessesByName(string processName, int gracefulTimeoutMs = 1500)
    {
        if (string.IsNullOrWhiteSpace(processName)) return 0;

        var cleanName = SanitizeProcessName(processName);
        Process[] processes;
        try
        {
            processes = Process.GetProcessesByName(cleanName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to find processes for '{ProcessName}'.", cleanName);
            return 0;
        }

        if (processes.Length == 0)
        {
            _logger.LogDebug("No running instances found for process '{ProcessName}'.", cleanName);
            return 0;
        }

        int terminatedCount = 0;
        foreach (var process in processes)
        {
            using (process)
            {
                try
                {
                    if (process.HasExited) continue;

                    bool gracefulCloseAttempted = false;
                    // If the process has an active GUI window, request graceful closure first
                    if (process.MainWindowHandle != IntPtr.Zero)
                    {
                        _logger.LogInformation("Attempting graceful CloseMainWindow for process {ProcessName} (PID {Pid})...", cleanName, process.Id);
                        gracefulCloseAttempted = process.CloseMainWindow();
                        if (gracefulCloseAttempted && process.WaitForExit(gracefulTimeoutMs))
                        {
                            _logger.LogInformation("Process {ProcessName} (PID {Pid}) exited gracefully.", cleanName, process.Id);
                            terminatedCount++;
                            continue;
                        }
                    }

                    // For background apps, tray apps without active windows (e.g. Spotify background engine), or unresponsive windows
                    if (!process.HasExited)
                    {
                        _logger.LogInformation("Terminating process {ProcessName} (PID {Pid}) via Kill...", cleanName, process.Id);
                        process.Kill(entireProcessTree: true);
                        process.WaitForExit(1000);
                        terminatedCount++;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to terminate instance of process {ProcessName} (PID {Pid}).", cleanName, process.Id);
                }
            }
        }

        return terminatedCount;
    }

    private static string SanitizeProcessName(string processName)
    {
        var trimmed = processName.Trim();
        if (trimmed.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
        {
            return trimmed[..^4];
        }
        return trimmed;
    }
}
