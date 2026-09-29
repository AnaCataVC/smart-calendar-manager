using System;
using Microsoft.Win32;
using SmartCalendarManager.Core.Services;

namespace SmartCalendarManager.Services;

/// <summary>
/// Production implementation of <see cref="IStartupRegistrationProvider"/> that interacts with
/// the Windows Registry under HKCU\Software\Microsoft\Windows\CurrentVersion\Run.
/// </summary>
public class WindowsRegistryStartupProvider : IStartupRegistrationProvider
{
    private const string RunSubKey = @"Software\Microsoft\Windows\CurrentVersion\Run";

    public bool IsRegistered(string appName)
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunSubKey, writable: false);
        return key?.GetValue(appName) is string;
    }

    public string? GetRegisteredCommand(string appName)
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunSubKey, writable: false);
        return key?.GetValue(appName) as string;
    }

    public void Register(string appName, string commandLine)
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunSubKey, writable: true)
            ?? Registry.CurrentUser.CreateSubKey(RunSubKey, writable: true);

        key?.SetValue(appName, commandLine, RegistryValueKind.String);
    }

    public void Unregister(string appName)
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunSubKey, writable: true);
        if (key != null && key.GetValue(appName) != null)
        {
            key.DeleteValue(appName, throwOnMissingValue: false);
        }
    }
}
