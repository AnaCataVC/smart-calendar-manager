using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;

namespace SmartCalendarManager.Core.Services;

public class AppCloserService : IAppCloserService
{
    private static readonly char[] Delimiters = { ',', ';', '\r', '\n' };

    private readonly IProcessController _processController;
    private readonly ILogger<AppCloserService> _logger;

    public AppCloserService(IProcessController processController, ILogger<AppCloserService> logger)
    {
        _processController = processController ?? throw new ArgumentNullException(nameof(processController));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public IReadOnlyList<string> ParseProcessNames(string? processListString)
    {
        if (string.IsNullOrWhiteSpace(processListString))
        {
            return Array.Empty<string>();
        }

        return processListString
            .Split(Delimiters, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(name => name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? name[..^4].Trim() : name)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public int CloseConfiguredApps(string? processListString)
    {
        var targetApps = ParseProcessNames(processListString);
        if (targetApps.Count == 0)
        {
            _logger.LogInformation("No target applications configured to close.");
            return 0;
        }

        _logger.LogInformation("Executing pre-meeting closure for configured applications: {Apps}", string.Join(", ", targetApps));

        int totalTerminated = 0;
        foreach (var appName in targetApps)
        {
            try
            {
                int terminated = _processController.TerminateProcessesByName(appName);
                if (terminated > 0)
                {
                    _logger.LogInformation("Closed {Count} instance(s) of '{AppName}'.", terminated, appName);
                }
                totalTerminated += terminated;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to close application '{AppName}'.", appName);
            }
        }

        _logger.LogInformation("Finished pre-meeting application closure. Total terminated instances: {Count}.", totalTerminated);
        return totalTerminated;
    }

    public IReadOnlyList<string> GetRunningConfiguredApps(string? processListString)
    {
        var targetApps = ParseProcessNames(processListString);
        if (targetApps.Count == 0) return Array.Empty<string>();

        var running = new List<string>();
        foreach (var app in targetApps)
        {
            if (_processController.IsProcessRunning(app))
            {
                running.Add(app);
            }
        }

        return running;
    }
}
