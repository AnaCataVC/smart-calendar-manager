using System;

namespace SmartCalendarManager.Core.Common;

/// <summary>
/// Helper class containing CLI flag constants and argument evaluation.
/// </summary>
public static class StartupArguments
{
    public const string Minimized = "--minimized";
    public const string Startup = "--startup";

    /// <summary>
    /// Returns true if the argument array contains either '--minimized' or '--startup' (case-insensitive).
    /// Supports both double and single dash prefixes as well as Windows slash prefix.
    /// </summary>
    public static bool ShouldStartMinimized(string[]? args)
    {
        if (args == null || args.Length == 0)
        {
            return false;
        }

        foreach (var arg in args)
        {
            if (string.IsNullOrWhiteSpace(arg))
            {
                continue;
            }

            var trimmed = arg.Trim();
            if (trimmed.Equals(Minimized, StringComparison.OrdinalIgnoreCase) ||
                trimmed.Equals("-minimized", StringComparison.OrdinalIgnoreCase) ||
                trimmed.Equals("/minimized", StringComparison.OrdinalIgnoreCase) ||
                trimmed.Equals(Startup, StringComparison.OrdinalIgnoreCase) ||
                trimmed.Equals("-startup", StringComparison.OrdinalIgnoreCase) ||
                trimmed.Equals("/startup", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
