using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Google;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Calendar.v3;
using Google.Apis.Calendar.v3.Data;
using Google.Apis.Services;
using Google.Apis.Util.Store;
using Microsoft.Extensions.Logging;
using SmartCalendarManager.Core.Helpers;
using SmartCalendarManager.Core.Models;

namespace SmartCalendarManager.Core.Services;

public interface IGoogleOAuthSyncService
{
    CalendarSyncSettings Settings { get; }
    bool IsAuthorized { get; }
    Task<bool> AuthorizeAsync(string clientId, string clientSecret, CancellationToken ct = default);
    Task<int> SynchronizeAvailabilityAsync(CancellationToken ct = default);
    void UpdateSettings(CalendarSyncSettings settings);
    Task SignOutAsync();
}

public class GoogleOAuthSyncService : IGoogleOAuthSyncService
{
    private const string SettingsKey = "GoogleOAuthCalendarSyncSettings";
    private const string AppName = "SmartCalendarManager";
    private const string SourceTagKey = "SourcePersonalEventId";

    private readonly IDataStore _dataStore;
    private readonly ILogger<GoogleOAuthSyncService> _logger;
    private CalendarSyncSettings _settings = new();
    private CalendarService? _calendarService;

    public CalendarSyncSettings Settings => _settings;
    public bool IsAuthorized => _calendarService != null;

    private static readonly string[] Scopes =
    {
        CalendarService.Scope.CalendarReadonly,
        CalendarService.Scope.CalendarEvents
    };

    public GoogleOAuthSyncService(
        IDataStore dataStore,
        ILogger<GoogleOAuthSyncService> logger)
    {
        _dataStore = dataStore;
        _logger = logger;

        LoadSettings();
    }

    private void LoadSettings()
    {
        var loaded = LocalSettingsHelper.LoadJson<CalendarSyncSettings>(SettingsKey);
        if (loaded != null)
        {
            _settings = loaded;
        }
    }

    public void UpdateSettings(CalendarSyncSettings settings)
    {
        _settings = settings ?? new CalendarSyncSettings();
        LocalSettingsHelper.SaveJson(SettingsKey, _settings);
    }

    public async Task<bool> AuthorizeAsync(string clientId, string clientSecret, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(clientSecret))
        {
            _logger.LogWarning("ClientId or ClientSecret missing for Google OAuth.");
            return false;
        }

        try
        {
            _settings.ClientId = clientId.Trim();
            _settings.ClientSecret = clientSecret.Trim();

            var secrets = new ClientSecrets
            {
                ClientId = _settings.ClientId,
                ClientSecret = _settings.ClientSecret
            };

            var credential = await GoogleWebAuthorizationBroker.AuthorizeAsync(
                secrets,
                Scopes,
                "user",
                ct,
                _dataStore,
                new LocalServerCodeReceiver());

            _calendarService = new CalendarService(new BaseClientService.Initializer
            {
                HttpClientInitializer = credential,
                ApplicationName = AppName
            });

            UpdateSettings(_settings);
            _logger.LogInformation("Google OAuth authorization succeeded.");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Google OAuth authorization failed.");
            _calendarService = null;
            return false;
        }
    }

    public async Task SignOutAsync()
    {
        await _dataStore.ClearAsync();
        _calendarService = null;
        _settings.LastSyncToken = string.Empty;
        _settings.EventMappings.Clear();
        UpdateSettings(_settings);
        _logger.LogInformation("Google OAuth signed out and token store cleared.");
    }

    public async Task<int> SynchronizeAvailabilityAsync(CancellationToken ct = default)
    {
        if (_calendarService == null)
        {
            var reauth = await AuthorizeAsync(_settings.ClientId, _settings.ClientSecret, ct);
            if (!reauth || _calendarService == null)
            {
                _logger.LogWarning("Cannot synchronize: User not authorized.");
                return 0;
            }
        }

        if (string.IsNullOrWhiteSpace(_settings.WorkCalendarId))
        {
            _logger.LogWarning("Work calendar ID not configured. Skipping availability sync.");
            return 0;
        }

        int changesCount = 0;
        try
        {
            var personalCalendarId = string.IsNullOrWhiteSpace(_settings.PersonalCalendarId) ? "primary" : _settings.PersonalCalendarId;

            if (string.IsNullOrEmpty(_settings.LastSyncToken))
            {
                changesCount = await ExecuteFullResyncAsync(personalCalendarId, _settings.WorkCalendarId, ct);
            }
            else
            {
                try
                {
                    changesCount = await ExecuteIncrementalSyncAsync(personalCalendarId, _settings.WorkCalendarId, ct);
                }
                catch (GoogleApiException ex) when (ex.HttpStatusCode == HttpStatusCode.Gone)
                {
                    _logger.LogWarning("SyncToken expired (410 Gone). Performing full resynchronization...");
                    _settings.LastSyncToken = string.Empty;
                    changesCount = await ExecuteFullResyncAsync(personalCalendarId, _settings.WorkCalendarId, ct);
                }
            }

            _settings.LastSyncTime = DateTime.UtcNow;
            UpdateSettings(_settings);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed during calendar availability synchronization.");
        }

        return changesCount;
    }

    private async Task<int> ExecuteFullResyncAsync(string personalCalId, string workCalId, CancellationToken ct)
    {
        int processedCount = 0;
        var request = _calendarService!.Events.List(personalCalId);
        request.TimeMinDateTimeOffset = DateTimeOffset.UtcNow.AddDays(-1);
        request.TimeMaxDateTimeOffset = DateTimeOffset.UtcNow.AddDays(30);
        request.SingleEvents = true;

        string? pageToken = null;
        do
        {
            request.PageToken = pageToken;
            var response = await request.ExecuteAsync(ct);

            if (response.Items != null)
            {
                foreach (var ev in response.Items)
                {
                    await ReconcilePersonalEventAsync(ev, workCalId, ct);
                    processedCount++;
                }
            }

            pageToken = response.NextPageToken;
            if (string.IsNullOrEmpty(pageToken) && !string.IsNullOrEmpty(response.NextSyncToken))
            {
                _settings.LastSyncToken = response.NextSyncToken;
            }
        } while (!string.IsNullOrEmpty(pageToken));

        return processedCount;
    }

    private async Task<int> ExecuteIncrementalSyncAsync(string personalCalId, string workCalId, CancellationToken ct)
    {
        int processedCount = 0;
        var request = _calendarService!.Events.List(personalCalId);
        request.SyncToken = _settings.LastSyncToken;

        string? pageToken = null;
        do
        {
            request.PageToken = pageToken;
            var response = await request.ExecuteAsync(ct);

            if (response.Items != null)
            {
                foreach (var ev in response.Items)
                {
                    await ReconcilePersonalEventAsync(ev, workCalId, ct);
                    processedCount++;
                }
            }

            pageToken = response.NextPageToken;
            if (string.IsNullOrEmpty(pageToken) && !string.IsNullOrEmpty(response.NextSyncToken))
            {
                _settings.LastSyncToken = response.NextSyncToken;
            }
        } while (!string.IsNullOrEmpty(pageToken));

        return processedCount;
    }

    private async Task ReconcilePersonalEventAsync(Event personalEv, string workCalId, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(personalEv.Id)) return;

        bool isCancelled = personalEv.Status != null && personalEv.Status.Equals("cancelled", StringComparison.OrdinalIgnoreCase);

        if (_settings.EventMappings.TryGetValue(personalEv.Id, out var mapping))
        {
            if (isCancelled)
            {
                try
                {
                    await _calendarService!.Events.Delete(workCalId, mapping.WorkCalendarEventId).ExecuteAsync(ct);
                    _logger.LogInformation("Deleted work block event {WorkId} for cancelled personal event {PersonalId}.", mapping.WorkCalendarEventId, personalEv.Id);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to delete work block event {WorkId}.", mapping.WorkCalendarEventId);
                }

                _settings.EventMappings.Remove(personalEv.Id);
            }
            else
            {
                try
                {
                    var blockEvent = await _calendarService!.Events.Get(workCalId, mapping.WorkCalendarEventId).ExecuteAsync(ct);
                    blockEvent.Summary = _settings.BlockEventTitle;
                    blockEvent.Start = personalEv.Start;
                    blockEvent.End = personalEv.End;
                    blockEvent.Transparency = "opaque";
                    blockEvent.Visibility = "private";

                    await _calendarService.Events.Update(blockEvent, workCalId, blockEvent.Id).ExecuteAsync(ct);
                    _logger.LogInformation("Updated work block event {WorkId} for personal event {PersonalId}.", blockEvent.Id, personalEv.Id);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to update work block event {WorkId}.", mapping.WorkCalendarEventId);
                }
            }
        }
        else
        {
            if (!isCancelled && personalEv.Start != null && personalEv.End != null)
            {
                try
                {
                    var blockEvent = new Event
                    {
                        Summary = _settings.BlockEventTitle,
                        Start = personalEv.Start,
                        End = personalEv.End,
                        Transparency = "opaque",
                        Visibility = "private",
                        ExtendedProperties = new Event.ExtendedPropertiesData
                        {
                            Private__ = new Dictionary<string, string>
                            {
                                [SourceTagKey] = personalEv.Id
                            }
                        }
                    };

                    var created = await _calendarService!.Events.Insert(blockEvent, workCalId).ExecuteAsync(ct);
                    _settings.EventMappings[personalEv.Id] = new SyncMappingRecord
                    {
                        PersonalEventId = personalEv.Id,
                        WorkCalendarEventId = created.Id,
                        LastSynchronizedUtc = DateTime.UtcNow
                    };

                    _logger.LogInformation("Created work block event {WorkId} for personal event {PersonalId}.", created.Id, personalEv.Id);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to create work block event for personal event {PersonalId}.", personalEv.Id);
                }
            }
        }
    }
}
