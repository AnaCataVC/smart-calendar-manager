using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
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
    IReadOnlyList<CalendarFeed> Feeds { get; }
    bool IsConfigured { get; }
    CalendarFilterSettings FilterSettings { get; }
    HashSet<string> DismissedAlertIds { get; }
    IReadOnlyList<string> FailedFeedNames { get; }
    event EventHandler<CalendarEvent>? UpcomingMeetingDetected;

    Task<bool> IsValidFeedAsync(string url, string? authKey = null);
    void SaveFeeds(IEnumerable<CalendarFeed> feeds);
    Task<List<CalendarEvent>> GetTodayEventsAsync();
    void ScheduleMeetingAlerts(IEnumerable<CalendarEvent> events);
    void ClearAlerts();
    void UpdateFilterSettings(CalendarFilterSettings settings);
    void DismissAlert(string eventId);
}

public class GoogleCalendarService : IGoogleCalendarService
{
    private const string CalendarFeedsKey = "CalendarFeeds";
    // Pre-multi-feed settings, migrated into a single Work feed on first load.
    private const string LegacyICalUrlSettingKey = "CalendarICalUrl";
    private const string LegacyICalKeySettingKey = "CalendarICalKey";
    private const string CalendarExcludedKeywordsKey = "CalendarExcludedKeywords";
    private const string CalendarIgnoreAllDayEventsKey = "CalendarIgnoreAllDayEvents";
    private const string CalendarRequireMeetingLinkKey = "CalendarRequireMeetingLink";
    private const string GranolaScopeKey = "GranolaScope";
    private const string DismissedAlertsKey = "CalendarDismissedAlerts";

    private readonly IAppLauncherService _appLauncherService;
    private readonly ILogger<GoogleCalendarService> _logger;
    private readonly HttpMessageHandler? _httpHandler;
    private readonly ConcurrentBag<Timer> _activeTimers = new();
    private readonly HashSet<string> _dismissedAlertIds = new(StringComparer.OrdinalIgnoreCase);

    private List<CalendarFeed> _feeds = new();
    private List<string> _failedFeedNames = new();
    private CalendarFilterSettings _filterSettings = new();

    public IReadOnlyList<CalendarFeed> Feeds => _feeds;
    public bool IsConfigured => _feeds.Any(f => f.Enabled && !string.IsNullOrWhiteSpace(f.Url));
    public CalendarFilterSettings FilterSettings => _filterSettings;
    public HashSet<string> DismissedAlertIds => _dismissedAlertIds;
    public IReadOnlyList<string> FailedFeedNames => _failedFeedNames;

    public event EventHandler<CalendarEvent>? UpcomingMeetingDetected;

    /// <param name="httpHandler">Optional transport override; null uses the default network stack.</param>
    public GoogleCalendarService(
        IAppLauncherService appLauncherService,
        ILogger<GoogleCalendarService> logger,
        HttpMessageHandler? httpHandler = null)
    {
        _appLauncherService = appLauncherService;
        _logger = logger;
        _httpHandler = httpHandler;

        LoadSavedSettings();
    }

    private void LoadSavedSettings()
    {
        try
        {
            _feeds = LocalSettingsHelper.LoadJson<List<CalendarFeed>>(CalendarFeedsKey) ?? MigrateLegacyFeed();

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

            if (Enum.TryParse<GranolaScope>(LocalSettingsHelper.Get(GranolaScopeKey), out var scope))
            {
                _filterSettings.GranolaScope = scope;
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

    private List<CalendarFeed> MigrateLegacyFeed()
    {
        var feeds = new List<CalendarFeed>();
        var legacyUrl = LocalSettingsHelper.Get(LegacyICalUrlSettingKey);
        if (!string.IsNullOrWhiteSpace(legacyUrl))
        {
            var legacyKey = LocalSettingsHelper.Get(LegacyICalKeySettingKey);
            feeds.Add(new CalendarFeed
            {
                Name = "Laboral",
                Url = legacyUrl,
                Kind = CalendarFeedKind.Work,
                AuthKey = string.IsNullOrWhiteSpace(legacyKey) ? null : legacyKey
            });
            _logger.LogInformation("Migrated the legacy single iCal URL into a Work feed.");
        }

        LocalSettingsHelper.SaveJson(CalendarFeedsKey, feeds);
        LocalSettingsHelper.Remove(LegacyICalUrlSettingKey);
        LocalSettingsHelper.Remove(LegacyICalKeySettingKey);
        return feeds;
    }

    public void SaveFeeds(IEnumerable<CalendarFeed> feeds)
    {
        _feeds = feeds.ToList();
        LocalSettingsHelper.SaveJson(CalendarFeedsKey, _feeds);
        _logger.LogInformation("Saved {Count} calendar feeds.", _feeds.Count);
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
        LocalSettingsHelper.Set(GranolaScopeKey, _filterSettings.GranolaScope.ToString());
        _logger.LogInformation("Calendar filter settings updated.");
    }

    private HttpClient CreateConfiguredHttpClient()
    {
        var client = _httpHandler == null ? new HttpClient() : new HttpClient(_httpHandler, disposeHandler: false);
        client.Timeout = TimeSpan.FromSeconds(15);
        client.DefaultRequestHeaders.CacheControl = new CacheControlHeaderValue
        {
            NoCache = true,
            NoStore = true,
            MustRevalidate = true
        };
        client.DefaultRequestHeaders.Pragma.Add(new NameValueHeaderValue("no-cache"));
        return client;
    }

    private static AuthenticationHeaderValue? BuildBasicAuthHeader(string? key)
    {
        if (string.IsNullOrWhiteSpace(key)) return null;
        var authBytes = Encoding.UTF8.GetBytes($"calendar:{key.Trim()}");
        return new AuthenticationHeaderValue("Basic", Convert.ToBase64String(authBytes));
    }

    // Auth goes on each request, not the shared client, because every feed may use a different key.
    private static async Task<string> DownloadFeedAsync(HttpClient client, string url, string? authKey)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = BuildBasicAuthHeader(authKey);
        using var response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }

    public async Task<bool> IsValidFeedAsync(string url, string? authKey = null)
    {
        if (string.IsNullOrWhiteSpace(url)) return false;

        try
        {
            using var client = CreateConfiguredHttpClient();
            var response = await DownloadFeedAsync(client, url.Trim(), authKey);
            return response.Contains("BEGIN:VCALENDAR", StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to download iCal feed while validating it.");
            return false;
        }
    }

    public async Task<List<CalendarEvent>> GetTodayEventsAsync()
    {
        var enabledFeeds = _feeds.Where(f => f.Enabled && !string.IsNullOrWhiteSpace(f.Url)).ToList();
        if (enabledFeeds.Count == 0)
        {
            _failedFeedNames = new List<string>();
            return new List<CalendarEvent>();
        }

        using var client = CreateConfiguredHttpClient();
        var perFeed = await Task.WhenAll(enabledFeeds.Select(feed => FetchFeedEventsAsync(client, feed)));

        _failedFeedNames = enabledFeeds.Where((_, i) => perFeed[i] == null).Select(f => f.Name).ToList();
        var result = perFeed
            .Where(events => events != null)
            .SelectMany(events => events!)
            .OrderBy(e => e.StartTime)
            .ToList();

        foreach (var ev in result)
        {
            ev.InGranolaScope = _filterSettings.IsInGranolaScope(ev);
            ev.OpensGranola = _filterSettings.ShouldOpenGranola(ev);
        }

        _logger.LogInformation("Parsed {Count} calendar events for today from {FeedCount} feeds ({GranolaCount} eligible for Granola).",
            result.Count, enabledFeeds.Count - _failedFeedNames.Count, result.Count(e => e.OpensGranola));

        ScheduleMeetingAlerts(result);
        return result;
    }

    /// <returns>The feed's events for today, or null when it could not be fetched or parsed.</returns>
    private async Task<List<CalendarEvent>?> FetchFeedEventsAsync(HttpClient client, CalendarFeed feed)
    {
        try
        {
            var icsContent = await DownloadFeedAsync(client, feed.Url, feed.AuthKey);
            var events = ICalParser.ParseEventsForDate(icsContent, DateTime.Today);
            foreach (var ev in events)
            {
                ev.FeedKind = feed.Kind;
                ev.FeedName = feed.Name;
            }
            return events;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve and parse iCal feed '{FeedName}'.", feed.Name);
            return null;
        }
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
