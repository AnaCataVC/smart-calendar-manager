using System;
using System.IO;
using System.Security;
using Microsoft.Extensions.Logging;
using SmartCalendarManager.Core.Common;

namespace SmartCalendarManager.Core.Services;

/// <summary>
/// Production implementation of <see cref="IStartupService"/> providing startup management,
/// path quotation enforcement, and fault-tolerant provider interaction.
/// </summary>
public class StartupService : IStartupService
{
    public const string DefaultAppName = "Smart Calendar Manager";

    private readonly IStartupRegistrationProvider _registrationProvider;
    private readonly ILogger<StartupService>? _logger;
    private readonly Func<string> _executablePathResolver;

    public string AppName { get; }
    public bool IsStartupEnabled { get; private set; }
    public bool LaunchMinimized { get; set; } = true;

    public StartupService(
        IStartupRegistrationProvider registrationProvider,
        ILogger<StartupService>? logger = null,
        Func<string>? executablePathResolver = null,
        string appName = DefaultAppName)
    {
        _registrationProvider = registrationProvider ?? throw new ArgumentNullException(nameof(registrationProvider));
        _logger = logger;
        _executablePathResolver = executablePathResolver ?? (() => Environment.ProcessPath ?? string.Empty);
        AppName = string.IsNullOrWhiteSpace(appName) ? DefaultAppName : appName;

        RefreshStatus();
    }

    public StartupService(
        IStartupRegistrationProvider registrationProvider,
        Func<string> executablePathResolver)
        : this(registrationProvider, null, executablePathResolver, DefaultAppName)
    {
    }

    public string GetCurrentExecutablePath()
    {
        return _executablePathResolver();
    }

    public void RefreshStatus()
    {
        try
        {
            if (!_registrationProvider.IsRegistered(AppName))
            {
                IsStartupEnabled = false;
                return;
            }

            var command = _registrationProvider.GetRegisteredCommand(AppName);
            if (command == null)
            {
                IsStartupEnabled = false;
                return;
            }

            IsStartupEnabled = true;
            LaunchMinimized = command.Contains(StartupArguments.Minimized, StringComparison.OrdinalIgnoreCase) ||
                              command.Contains(StartupArguments.Startup, StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to refresh startup status for '{AppName}'.", AppName);
            IsStartupEnabled = false;
        }
    }

    public bool SetStartup(bool enable, bool launchMinimized = true)
    {
        try
        {
            if (enable)
            {
                var rawPath = GetCurrentExecutablePath()?.Trim() ?? string.Empty;
                if (string.IsNullOrWhiteSpace(rawPath))
                {
                    _logger?.LogWarning("Cannot enable startup for '{AppName}': executable path is empty.", AppName);
                    return false;
                }

                var unquotedPath = rawPath.Trim('"');
                var quotedPath = $"\"{unquotedPath}\"";
                var command = launchMinimized
                    ? $"{quotedPath} {StartupArguments.Minimized}"
                    : quotedPath;

                _registrationProvider.Register(AppName, command);
                IsStartupEnabled = true;
                LaunchMinimized = launchMinimized;
                return true;
            }
            else
            {
                _registrationProvider.Unregister(AppName);
                IsStartupEnabled = false;
                return true;
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to update startup registration for '{AppName}'.", AppName);
            return false;
        }
    }
}
