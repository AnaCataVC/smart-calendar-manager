using System;
using System.Collections.ObjectModel;
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
        IAppLauncherService appLauncher)
    {
        _calendarService = calendarService;
        _appLauncher = appLauncher;

        _calendarService.UpcomingMeetingDetected += OnUpcomingMeetingDetected;
    }

    private void OnUpcomingMeetingDetected(object? sender, CalendarEvent ev)
    {
        if (!_calendarService.DismissedAlertIds.Contains(ev.Id))
        {
            ActiveBannerEvent = ev;
            IsBannerVisible = true;
        }
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

            StatusMessage = TodayEvents.Count > 0
                ? $"{TodayEvents.Count} reuniones encontradas para hoy"
                : "No hay reuniones programadas para hoy";
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
