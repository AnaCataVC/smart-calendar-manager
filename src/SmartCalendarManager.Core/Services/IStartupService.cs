namespace SmartCalendarManager.Core.Services;

/// <summary>
/// High-level domain service managing startup behavior, command-line sanitization, and state synchronization.
/// </summary>
public interface IStartupService
{
    /// <summary>
    /// Gets the unique application name key used in system startup registries (default: "Smart Calendar Manager").
    /// </summary>
    string AppName { get; }

    /// <summary>
    /// Indicates whether the application is currently configured to launch at Windows startup.
    /// </summary>
    bool IsStartupEnabled { get; }

    /// <summary>
    /// Indicates whether the application should launch minimized to the system tray when auto-started.
    /// </summary>
    bool LaunchMinimized { get; set; }

    /// <summary>
    /// Enables or disables automatic startup with Windows.
    /// </summary>
    /// <param name="enable">True to register startup, false to unregister.</param>
    /// <param name="launchMinimized">True to include the minimized launch flag.</param>
    /// <returns>True if the operation succeeded, false if an error occurred.</returns>
    bool SetStartup(bool enable, bool launchMinimized = true);

    /// <summary>
    /// Re-evaluates registration status and parameters from the underlying provider.
    /// </summary>
    void RefreshStatus();

    /// <summary>
    /// Resolves the current executable path to register.
    /// </summary>
    string GetCurrentExecutablePath();
}
