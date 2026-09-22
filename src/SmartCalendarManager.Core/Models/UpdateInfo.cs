namespace SmartCalendarManager.Core.Models;

public class UpdateInfo
{
    public string CurrentVersion { get; set; } = string.Empty;
    public string LatestVersion { get; set; } = string.Empty;
    public bool IsUpdateAvailable { get; set; }
    public string ReleaseHtmlUrl { get; set; } = string.Empty;
    public string DownloadUrl { get; set; } = string.Empty;
    public string InstallerFileName { get; set; } = string.Empty;
    public string? ErrorMessage { get; set; }
}
