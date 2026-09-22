using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace SmartCalendarManager.Core.Helpers;

/// <summary>
/// Helper for storing and retrieving flat settings and JSON objects in %LOCALAPPDATA%\SmartCalendarManager\Data\settings.json.
/// Thread-safe and supports redirection for unit test isolation.
/// </summary>
public static class LocalSettingsHelper
{
    private static readonly object LockObj = new();
    private static string? _customSettingsPath;

    public static string SettingsFilePath
    {
        get
        {
            if (!string.IsNullOrEmpty(_customSettingsPath))
            {
                return _customSettingsPath;
            }

            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            return Path.Combine(localAppData, "SmartCalendarManager", "Data", "settings.json");
        }
        set => _customSettingsPath = value;
    }

    public static void ResetToDefaultPath()
    {
        _customSettingsPath = null;
    }

    public static string? Get(string key)
    {
        lock (LockObj)
        {
            var dict = LoadDictionary();
            return dict.TryGetValue(key, out var val) ? val : null;
        }
    }

    public static void Set(string key, string value)
    {
        lock (LockObj)
        {
            var dict = LoadDictionary();
            dict[key] = value;
            SaveDictionary(dict);
        }
    }

    public static void Remove(string key)
    {
        lock (LockObj)
        {
            var dict = LoadDictionary();
            if (dict.Remove(key))
            {
                SaveDictionary(dict);
            }
        }
    }

    public static T? LoadJson<T>(string key)
    {
        var raw = Get(key);
        if (string.IsNullOrWhiteSpace(raw)) return default;

        try
        {
            return JsonSerializer.Deserialize<T>(raw);
        }
        catch
        {
            return default;
        }
    }

    public static void SaveJson<T>(string key, T value)
    {
        var json = JsonSerializer.Serialize(value, new JsonSerializerOptions { WriteIndented = true });
        Set(key, json);
    }

    private static Dictionary<string, string> LoadDictionary()
    {
        try
        {
            var path = SettingsFilePath;
            if (!File.Exists(path)) return new Dictionary<string, string>();

            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? new Dictionary<string, string>();
        }
        catch
        {
            return new Dictionary<string, string>();
        }
    }

    private static void SaveDictionary(Dictionary<string, string> dict)
    {
        try
        {
            var path = SettingsFilePath;
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            var json = JsonSerializer.Serialize(dict, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(path, json);
        }
        catch
        {
            // Ignore settings write errors gracefully
        }
    }
}
