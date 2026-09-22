using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using SmartCalendarManager.Core.Helpers;
using SmartCalendarManager.Core.Models;

namespace SmartCalendarManager.Core.Services;

public interface IGoogleCalendarService : IDisposable
{
    string? ICalUrl { get; }
    string? ICalKey { get; }
    bool IsConfigured { get; }
    CalendarFilterSettings FilterSettings { get; }
    HashSet<string> DismissedAlertIds { get; }
    event EventHandler<CalendarEvent>? UpcomingMeetingDetected;

    Task<bool> SetICalCredentialsAsync(string url, string? key = null);
    Task ClearICalCredentialsAsync();
    Task<List<CalendarEvent>> GetTodayEventsAsync();
    void ScheduleMeetingAlerts(IEnumerable<CalendarEvent> events);
    void ClearAlerts();
    void UpdateFilterSettings(CalendarFilterSettings settings);
    void DismissAlert(string eventId);
}

public class GoogleCalendarService : IGoogleCalendarService
{
    private const string ICalUrlSettingKey = "CalendarICalUrl";
    private const string ICalKeySettingKey = "CalendarICalKey";
    private const string CalendarExcludedKeywordsKey = "CalendarExcludedKeywords";
    private const string CalendarIgnoreAllDayEventsKey = "CalendarIgnoreAllDayEvents";
    private const string CalendarRequireMeetingLinkKey = "CalendarRequireMeetingLink";
    private const string DismissedAlertsKey = "CalendarDismissedAlerts";

    private readonly IAppLauncherService _appLauncherService;
    private readonly ILogger<GoogleCalendarService> _logger;
    private readonly ConcurrentBag<Timer> _activeTimers = new();
    private readonly HashSet<string> _dismissedAlertIds = new(StringComparer.OrdinalIgnoreCase);

    private string? _iCalUrl;
    private string? _iCalKey;
    private CalendarFilterSettings _filterSettings = new();

    public string? ICalUrl => _iCalUrl;
    public string? ICalKey => _iCalKey;
    public bool IsConfigured => !string.IsNullOrWhiteSpace(_iCalUrl);
    public CalendarFilterSettings FilterSettings => _filterSettings;
    public HashSet<string> DismissedAlertIds => _dismissedAlertIds;

    public event EventHandler<CalendarEvent>? UpcomingMeetingDetected;

    public GoogleCalendarService(
        IAppLauncherService appLauncherService,
        ILogger<GoogleCalendarService> logger)
    {
        _appLauncherService = appLauncherService;
        _logger = logger;

        LoadSavedSettings();
    }

    private void LoadSavedSettings()
    {
        try
        {
            _iCalUrl = LocalSettingsHelper.Get(ICalUrlSettingKey);
            _iCalKey = LocalSettingsHelper.Get(ICalKeySettingKey);

            var excludedKeywords = LocalSettingsHelper.Get(CalendarExcludedKeywordsKey);
            if (excludedKeywords != null)
            {
                _filterSettings.ExcludedKeywords = excludedKeywords;
            }

            var ignoreAllDayStr = LocalSettingsHelper.Get(CalendarIgnoreAllDayEventsKey);
            if (bool.TryParse(ignoreAllDayStr, out var ignoreAllDay))
            {
                _filterSettings.IgnoreAllDayEvents = ignoreAllDay;
            }

            var requireLinkStr = LocalSettingsHelper.Get(CalendarRequireMeetingLinkKey);
            if (bool.TryParse(requireLinkStr, out var requireLink))
            {
                _filterSettings.RequireMeetingLink = requireLink;
            }

            var dismissed = LocalSettingsHelper.LoadJson<List<string>>(DismissedAlertsKey);
            if (dismissed != null)
            {
                foreach (var id in dismissed)
                {
                    _dismissedAlertIds.Add(id);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load saved calendar settings.");
        }
    }

    public void DismissAlert(string eventId)
    {
        if (string.IsNullOrWhiteSpace(eventId)) return;

        _dismissedAlertIds.Add(eventId);
        LocalSettingsHelper.SaveJson(DismissedAlertsKey, new List<string>(_dismissedAlertIds));
        _logger.LogInformation("Alert dismissed and persisted for event ID {EventId}.", eventId);
    }

    public void UpdateFilterSettings(CalendarFilterSettings settings)
    {
        _filterSettings = settings ?? new CalendarFilterSettings();
        LocalSettingsHelper.Set(CalendarExcludedKeywordsKey, _filterSettings.ExcludedKeywords);
        LocalSettingsHelper.Set(CalendarIgnoreAllDayEventsKey, _filterSettings.IgnoreAllDayEvents.ToString());
        LocalSettingsHelper.Set(CalendarRequireMeetingLinkKey, _filterSettings.RequireMeetingLink.ToString());
        _logger.LogInformation("Calendar filter settings updated.");
    }

    private static AuthenticationHeaderValue? BuildBasicAuthHeader(string? key)
    {
        if (string.IsNullOrWhiteSpace(key)) return null;
        var authBytes = Encoding.UTF8.GetBytes($"calendar:{key.Trim()}");
        return new AuthenticationHeaderValue("Basic", Convert.ToBase64String(authBytes));
    }

    private HttpClient CreateConfiguredHttpClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        client.DefaultRequestHeaders.CacheControl = new CacheControlHeaderValue
        {
            NoCache = true,
            NoStore = true,
            MustRevalidate = true
        };
        client.DefaultRequestHeaders.Pragma.Add(new NameValueHeaderValue("no-cache"));

        var authHeader = BuildBasicAuthHeader(_iCalKey);
        if (authHeader != null)
        {
            client.DefaultRequestHeaders.Authorization = authHeader;
        }

        return client;
    }

    public async Task<bool> SetICalCredentialsAsync(string url, string? key = null)
    {
        if (string.IsNullOrWhiteSpace(url)) return false;

        url = url.Trim();
        if (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
            !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            url = "https://" + url;
        }

        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
            var authHeader = BuildBasicAuthHeader(key);
            if (authHeader != null) client.DefaultRequestHeaders.Authorization = authHeader;

            var response = await client.GetStringAsync(url);
            if (!response.Contains("BEGIN:VCALENDAR", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning("The provided URL did not return a valid iCalendar feed.");
                return false;
            }

            _iCalUrl = url;
            _iCalKey = string.IsNullOrWhiteSpace(key) ? null : key.Trim();

            LocalSettingsHelper.Set(ICalUrlSettingKey, _iCalUrl);
            if (_iCalKey != null) LocalSettingsHelper.Set(ICalKeySettingKey, _iCalKey);
            else LocalSettingsHelper.Remove(ICalKeySettingKey);

            _logger.LogInformation("iCal feed URL and credentials verified.");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to download iCal feed from URL.");
            return false;
        }
    }

    public async Task ClearICalCredentialsAsync()
    {
        _iCalUrl = null;
        _iCalKey = null;
        LocalSettingsHelper.Remove(ICalUrlSettingKey);
        LocalSettingsHelper.Remove(ICalKeySettingKey);
        ClearAlerts();
        await Task.CompletedTask;
    }

    public async Task<List<CalendarEvent>> GetTodayEventsAsync()
    {
        var result = new List<CalendarEvent>();
        if (string.IsNullOrWhiteSpace(_iCalUrl)) return result;

        try
        {
            using var client = CreateConfiguredHttpClient();
            var icsContent = await client.GetStringAsync(_iCalUrl);
            result = ICalParser.ParseEventsForDate(icsContent, DateTime.Today);

            foreach (var ev in result)
            {
                ev.OpensGranola = _filterSettings.ShouldOpenGranola(ev);
            }

            _logger.LogInformation("Parsed {Count} calendar events for today ({GranolaCount} eligible for Granola).",
                result.Count, result.FindAll(e => e.OpensGranola).Count);

            ScheduleMeetingAlerts(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve and parse iCal events.");
        }

        return result;
    }

    public void ScheduleMeetingAlerts(IEnumerable<CalendarEvent> events)
    {
        ClearAlerts();
        var now = DateTime.Now;

        foreach (var meeting in events)
        {
            if (!meeting.OpensGranola) continue;
            if (_dismissedAlertIds.Contains(meeting.Id))
            {
                _logger.LogInformation("Skipping alert for dismissed meeting '{Title}'.", meeting.Title);
                continue;
            }

            var granolaAlertTime = meeting.StartTime.AddMinutes(-5);
            var granolaDelay = granolaAlertTime - now;

            if (granolaDelay > TimeSpan.Zero)
            {
                _logger.LogInformation("Scheduling Granola alert for '{Title}' at {AlertTime} (in {DelayMinutes:F1} min).",
                    meeting.Title, granolaAlertTime, granolaDelay.TotalMinutes);

                var granolaTimer = new Timer(_ =>
                {
                    _logger.LogInformation("Upcoming meeting alert fired for '{Title}'. Ensuring Granola is open...", meeting.Title);
                    UpcomingMeetingDetected?.Invoke(this, meeting);
                    _appLauncherService.EnsureGranolaRunning();
                }, null, granolaDelay, Timeout.InfiniteTimeSpan);

                _activeTimers.Add(granolaTimer);
            }
            else if (now >= meeting.StartTime.AddMinutes(-5) && now < meeting.StartTime)
            {
                _logger.LogInformation("Meeting '{Title}' is in less than 5 minutes. Ensuring Granola immediately.", meeting.Title);
                UpcomingMeetingDetected?.Invoke(this, meeting);
                _appLauncherService.EnsureGranolaRunning();
            }
        }
    }

    public void ClearAlerts()
    {
        while (_activeTimers.TryTake(out var timer))
        {
            timer.Dispose();
        }
    }

    public void Dispose()
    {
        ClearAlerts();
    }
}
