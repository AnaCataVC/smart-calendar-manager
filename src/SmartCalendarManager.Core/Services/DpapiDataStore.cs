using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Google.Apis.Util.Store;

namespace SmartCalendarManager.Core.Services;

/// <summary>
/// Secure IDataStore implementation for Google API tokens using Windows Data Protection API (DPAPI).
/// Eliminates plain-text token exposure and isolates storage to current Windows user session.
/// </summary>
public class DpapiDataStore : IDataStore
{
    private readonly string _storageFolder;

    public DpapiDataStore(string subfolderName = "OAuthTokens")
    {
        _storageFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SmartCalendarManager",
            subfolderName);

        if (!Directory.Exists(_storageFolder))
        {
            Directory.CreateDirectory(_storageFolder);
        }
    }

    public Task StoreAsync<T>(string key, T value)
    {
        if (string.IsNullOrEmpty(key)) throw new ArgumentException("Key cannot be null or empty", nameof(key));

        var json = JsonSerializer.Serialize(value);
        var plainBytes = Encoding.UTF8.GetBytes(json);
        var encryptedBytes = ProtectedData.Protect(
            plainBytes,
            optionalEntropy: null,
            DataProtectionScope.CurrentUser);

        var filePath = GetFilePath(key);
        File.WriteAllBytes(filePath, encryptedBytes);
        return Task.CompletedTask;
    }

    public Task<T?> GetAsync<T>(string key)
    {
        if (string.IsNullOrEmpty(key)) throw new ArgumentException("Key cannot be null or empty", nameof(key));

        var filePath = GetFilePath(key);
        if (!File.Exists(filePath))
        {
            return Task.FromResult<T?>(default);
        }

        try
        {
            var encryptedBytes = File.ReadAllBytes(filePath);
            var plainBytes = ProtectedData.Unprotect(
                encryptedBytes,
                optionalEntropy: null,
                DataProtectionScope.CurrentUser);

            var json = Encoding.UTF8.GetString(plainBytes);
            var obj = JsonSerializer.Deserialize<T>(json);
            return Task.FromResult(obj);
        }
        catch
        {
            return Task.FromResult<T?>(default);
        }
    }

    public Task DeleteAsync<T>(string key)
    {
        if (string.IsNullOrEmpty(key)) throw new ArgumentException("Key cannot be null or empty", nameof(key));

        var filePath = GetFilePath(key);
        if (File.Exists(filePath))
        {
            try
            {
                File.Delete(filePath);
            }
            catch
            {
                // Ignore deletion errors
            }
        }

        return Task.CompletedTask;
    }

    public Task ClearAsync()
    {
        if (Directory.Exists(_storageFolder))
        {
            try
            {
                var files = Directory.GetFiles(_storageFolder, "*.dat");
                foreach (var f in files)
                {
                    File.Delete(f);
                }
            }
            catch
            {
                // Ignore clear errors
            }
        }

        return Task.CompletedTask;
    }

    private string GetFilePath(string key)
    {
        var safeKey = string.Join("_", key.Split(Path.GetInvalidFileNameChars()));
        return Path.Combine(_storageFolder, $"{safeKey}.dat");
    }
}
