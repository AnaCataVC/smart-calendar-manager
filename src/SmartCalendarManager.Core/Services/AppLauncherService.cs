using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Microsoft.Extensions.Logging;
using SmartCalendarManager.Core.Helpers;

namespace SmartCalendarManager.Core.Services;

public interface IAppLauncherService
{
    bool IsSlackRunning();
    bool IsGranolaRunning();
    void EnsureSlackRunning();
    void EnsureGranolaRunning();
    void LaunchSlack();
    void LaunchGranola();
    string? GetGranolaExecutablePath();
    void OpenUrl(string? url);
    void LaunchTarget(string target, string? arguments = null);
}

public class AppLauncherService : IAppLauncherService
{
    private readonly ILogger<AppLauncherService> _logger;

    public AppLauncherService(ILogger<AppLauncherService> logger)
    {
        _logger = logger;
    }

    public bool IsSlackRunning()
    {
        try { return Process.GetProcessesByName("slack").Length > 0; }
        catch { return false; }
    }

    public bool IsGranolaRunning()
    {
        try { return Process.GetProcessesByName("Granola").Length > 0; }
        catch { return false; }
    }

    public void EnsureSlackRunning()
    {
        if (!IsSlackRunning())
        {
            _logger.LogInformation("Slack is not running. Launching Slack...");
            LaunchSlack();
        }
        else
        {
            _logger.LogInformation("Slack is already running.");
        }
    }

    public void EnsureGranolaRunning()
    {
        if (!IsGranolaRunning())
        {
            _logger.LogInformation("Granola is not running. Launching Granola...");
            LaunchGranola();
        }
        else
        {
            _logger.LogInformation("Granola is already running.");
        }
    }

    public void LaunchSlack()
    {
        try
        {
            ProcessLaunchHelper.ShellExecute("slack:");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to launch Slack.");
        }
    }

    public string? GetGranolaExecutablePath()
    {
        string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        string programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        string programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);

        string[] candidates =
        {
            Path.Combine(localAppData, "Programs", "@granolaelectron", "Granola.exe"),
            Path.Combine(localAppData, "Programs", "Granola", "Granola.exe"),
            Path.Combine(localAppData, "Granola", "Granola.exe"),
            Path.Combine(programFiles, "Granola", "Granola.exe"),
            Path.Combine(programFilesX86, "Granola", "Granola.exe")
        };

        return candidates.FirstOrDefault(File.Exists);
    }

    public void LaunchGranola()
    {
        try
        {
            string? granolaPath = GetGranolaExecutablePath();

            if (!string.IsNullOrEmpty(granolaPath))
            {
                _logger.LogInformation("Launching Granola from: {Path}", granolaPath);
                ProcessLaunchHelper.ShellExecute(granolaPath);
                return;
            }

            _logger.LogInformation("Attempting to launch Granola via URI scheme 'granola:'...");
            try
            {
                ProcessLaunchHelper.ShellExecute("granola:");
                return;
            }
            catch (Exception uriEx)
            {
                _logger.LogWarning(uriEx, "Failed to launch Granola via URI scheme.");
            }

            _logger.LogInformation("Attempting fallback launch using 'Granola.exe'...");
            ProcessLaunchHelper.ShellExecute("Granola.exe");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to launch Granola.");
        }
    }

    public void OpenUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return;

        try
        {
            ProcessLaunchHelper.ShellExecute(url);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to open URL {Url}.", url);
        }
    }

    public void LaunchTarget(string target, string? arguments = null)
    {
        if (string.IsNullOrWhiteSpace(target)) return;

        try
        {
            _logger.LogInformation("Executing launch target: {Target} with args: {Args}", target, arguments);
            ProcessLaunchHelper.ShellExecute(target, arguments);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to execute target: {Target}", target);
        }
    }
}
