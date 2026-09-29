namespace SmartCalendarManager.Core.Services;

/// <summary>
/// Low-level abstraction for interacting with operating system autolaunch entries.
/// Decouples production code from Windows Registry APIs to enable in-memory testing.
/// </summary>
public interface IStartupRegistrationProvider
{
    /// <summary>
    /// Checks whether an autolaunch entry exists for the given application name.
    /// </summary>
    bool IsRegistered(string appName);

    /// <summary>
    /// Gets the registered command line string for the application, or null if not registered.
    /// </summary>
    string? GetRegisteredCommand(string appName);

    /// <summary>
    /// Creates or updates the autolaunch registration with the specified command line.
    /// </summary>
    void Register(string appName, string commandLine);

    /// <summary>
    /// Removes the autolaunch registration if it exists.
    /// </summary>
    void Unregister(string appName);
}
