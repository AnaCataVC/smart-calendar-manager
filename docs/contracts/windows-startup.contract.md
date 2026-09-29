# Interface & Behavioral Contract: Windows Startup & Autolaunch Service

**Feature Slug:** `windows-startup`  
**Status:** Frozen  
**Target Projects:** `SmartCalendarManager.Core`, `SmartCalendarManager.App`, `SmartCalendarManager.Tests`  

---

## 1. Scope & Objective

Provide configuration and execution support for launching Smart Calendar Manager automatically upon Windows user logon, with an optional minimized state (starting directly in the system tray without popping up the main window) in an unpackaged WinUI 3 desktop application (.NET 9).

---

## 2. Public Interfaces & Signatures

All types belong to namespace `SmartCalendarManager.Core.Services` or `SmartCalendarManager.Core.Common`.

### 2.1. `IStartupRegistrationProvider`
Low-level abstraction for interacting with operating system autolaunch entries. Decouples production code from Windows Registry APIs to enable 100% in-memory unit testing.

```csharp
namespace SmartCalendarManager.Core.Services;

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
```

### 2.2. `IStartupService`
High-level domain service managing startup behavior, command-line sanitization, and state synchronization.

```csharp
namespace SmartCalendarManager.Core.Services;

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
```

### 2.3. `StartupArguments`
Helper class containing CLI flag constants and argument evaluation.

```csharp
namespace SmartCalendarManager.Core.Common;

public static class StartupArguments
{
    public const string Minimized = "--minimized";
    public const string Startup = "--startup";

    /// <summary>
    /// Returns true if the argument array contains either '--minimized' or '--startup' (case-insensitive).
    /// </summary>
    public static bool ShouldStartMinimized(string[]? args);
}
```

---

## 3. Behavioral Acceptance Criteria & Invariants

1. **Path Quoting (Security & Reliability Invariant):**
   - The registered command line string MUST always encapsulate the executable path in double quotes (`"\"<Path>\""` or `"\"<Path>\" --minimized"`), especially when the path contains spaces.
2. **Minimization Flag Rules:**
   - When `enable = true` and `launchMinimized = true`, the registered command line MUST end with the `--minimized` argument preceded by a space: `"\"<ExePath>\" --minimized"`.
   - When `enable = true` and `launchMinimized = false`, the registered command line MUST be only the quoted path: `"\"<ExePath>\""`.
3. **Idempotence & Safe De-registration:**
   - Calling `SetStartup(true)` when already registered updates the command line if the minimized flag changed.
   - Calling `SetStartup(false)` when not registered executes cleanly without throwing exceptions.
4. **Resilience & Exception Handling:**
   - If `IStartupRegistrationProvider` throws `UnauthorizedAccessException`, `SecurityException`, or `InvalidOperationException`, `SetStartup` MUST catch the exception, log it, and return `false` without crashing the application.
5. **State Synchronization (`RefreshStatus`):**
   - If registered command contains `--minimized` (case-insensitive), `IsStartupEnabled` becomes `true` and `LaunchMinimized` becomes `true`.
   - If registered command exists but does not contain `--minimized`, `IsStartupEnabled` becomes `true` and `LaunchMinimized` becomes `false`.
   - If not registered, `IsStartupEnabled` becomes `false`.
6. **Argument Parsing (`StartupArguments.ShouldStartMinimized`):**
   - Returns `true` if `args` contains `--minimized`, `-minimized`, `--startup`, or `-startup` (case-insensitive).
   - Returns `false` for `null`, empty arrays, or unrelated arguments (`--help`, `--update`, etc.).
7. **Cleanroom Boundary Constraint:**
   - `SmartCalendarManager.Core` MUST NOT reference UI libraries (`Microsoft.UI.Xaml`, `WinUI`).
   - Unit tests MUST use an in-memory test double of `IStartupRegistrationProvider` to prevent any writes to the real OS registry during testing.

---

## 4. Acceptance Criteria (Gherkin Scenarios)

### Scenario 1: Enable startup with default minimized flag
- **Given** the application is not registered for startup
- **When** `SetStartup(enable: true, launchMinimized: true)` is called with executable path `"C:\Programs\SmartCalendarManager\SmartCalendarManager.App.exe"`
- **Then** the provider registers `"Smart Calendar Manager"` with command `"\"C:\Programs\SmartCalendarManager\SmartCalendarManager.App.exe\" --minimized"`
- **And** `IsStartupEnabled` is `true`
- **And** `LaunchMinimized` is `true`
- **And** the method returns `true`

### Scenario 2: Enable startup without minimized flag
- **Given** the application is not registered for startup
- **When** `SetStartup(enable: true, launchMinimized: false)` is called
- **Then** the provider registers command `"\"C:\Programs\SmartCalendarManager\SmartCalendarManager.App.exe\""`
- **And** `IsStartupEnabled` is `true`
- **And** `LaunchMinimized` is `false`
- **And** the method returns `true`

### Scenario 3: Disable startup
- **Given** the application is registered for startup in the provider
- **When** `SetStartup(enable: false)` is called
- **Then** the provider unregisters `"Smart Calendar Manager"`
- **And** `IsStartupEnabled` is `false`
- **And** the method returns `true`

### Scenario 4: CLI argument detection
- **Given** command line arguments `["--minimized"]` or `["--startup"]`
- **When** `StartupArguments.ShouldStartMinimized(args)` is evaluated
- **Then** it returns `true`
- **Given** command line arguments `null`, `[]`, or `["--other"]`
- **Then** it returns `false`

### Scenario 5: Handle provider permission errors gracefully
- **Given** the provider throws an `UnauthorizedAccessException` on `Register` or `Unregister`
- **When** `SetStartup(enable: true)` or `SetStartup(enable: false)` is called
- **Then** the method returns `false`
- **And** no unhandled exception is thrown
