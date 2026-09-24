using System;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SmartCalendarManager.Core.Models;
using SmartCalendarManager.Core.Services;

namespace SmartCalendarManager.Core.ViewModels;

public partial class AgendaViewModel : ObservableObject
{
    private readonly IGoogleCalendarService _calendarService;
    private readonly IAppLauncherService _appLauncher;
    // Captured on the UI thread; used to marshal banner mutations back to UI thread
    // from the background System.Threading.Timer that fires UpcomingMeetingDetected.
    private readonly SynchronizationContext? _syncContext;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private CalendarEvent? _activeBannerEvent;

    [ObservableProperty]
    private bool _isBannerVisible;

    public ObservableCollection<CalendarEvent> TodayEvents { get; } = new();

    public AgendaViewModel(
        IGoogleCalendarService calendarService,
        IAppLauncherService appLauncher,
        SynchronizationContext? syncContext = null,
        bool disableSyncContext = false)
    {
        _calendarService = calendarService;
        _appLauncher = appLauncher;
        _syncContext = disableSyncContext ? null : (syncContext ?? SynchronizationContext.Current);

        _calendarService.UpcomingMeetingDetected += OnUpcomingMeetingDetected;
    }

    private void OnUpcomingMeetingDetected(object? sender, CalendarEvent ev)
    {
        void UpdateBanner()
        {
            if (!_calendarService.DismissedAlertIds.Contains(ev.Id))
            {
                ActiveBannerEvent = ev;
                IsBannerVisible = true;
            }
        }

        if (_syncContext != null)
            _syncContext.Post(_ => UpdateBanner(), null);
        else
            UpdateBanner();
    }


    [RelayCommand]
    public async Task RefreshEventsAsync()
    {
        IsLoading = true;
        StatusMessage = "Actualizando agenda...";
        try
        {
            var events = await _calendarService.GetTodayEventsAsync();
            TodayEvents.Clear();
            foreach (var ev in events)
            {
                TodayEvents.Add(ev);
            }

            if (TodayEvents.Count > 0)
            {
                StatusMessage = $"{TodayEvents.Count} reuniones encontradas para hoy";
            }
            else if (!_calendarService.IsConfigured)
            {
                StatusMessage = "No hay calendarios configurados. Ve a Configuración para agregar tus URLs iCal.";
            }
            else
            {
                StatusMessage = "No hay reuniones programadas para hoy";
            }

            var failed = _calendarService.FailedFeedNames;
            if (failed is { Count: > 0 })
            {
                StatusMessage += $" (no se pudo leer: {string.Join(", ", failed)})";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error al cargar reuniones: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    public void JoinMeeting(CalendarEvent? ev)
    {
        if (ev != null && !string.IsNullOrWhiteSpace(ev.MeetingLink))
        {
            _appLauncher.OpenUrl(ev.MeetingLink);
        }
    }

    [RelayCommand]
    public void OpenGranola(CalendarEvent? ev)
    {
        _appLauncher.LaunchGranola();
        JoinMeeting(ev);
    }

    [RelayCommand]
    public void DismissBanner()
    {
        if (ActiveBannerEvent != null)
        {
            _calendarService.DismissAlert(ActiveBannerEvent.Id);
        }
        IsBannerVisible = false;
        ActiveBannerEvent = null;
    }
}
