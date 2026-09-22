using System;
using System.Diagnostics;

namespace SmartCalendarManager.Core.Helpers;

/// <summary>
/// Helper for running processes with shell execution.
/// </summary>
public static class ProcessLaunchHelper
{
    public static Process? ShellExecute(string fileName, string? arguments = null)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = fileName,
                UseShellExecute = true
            };

            if (!string.IsNullOrWhiteSpace(arguments))
            {
                psi.Arguments = arguments;
            }

            return Process.Start(psi);
        }
        catch
        {
            return null;
        }
    }
}
