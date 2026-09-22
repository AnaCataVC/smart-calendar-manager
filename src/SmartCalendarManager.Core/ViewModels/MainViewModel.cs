using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SmartCalendarManager.Core.Services;

namespace SmartCalendarManager.Core.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly IGoogleCalendarService _calendarService;
    private readonly IGoogleOAuthSyncService _oauthSyncService;

    [ObservableProperty]
    private string _title = "Smart Calendar Manager";

    public AgendaViewModel Agenda { get; }
    public SyncSettingsViewModel SyncSettings { get; }
    public CronLauncherViewModel CronLauncher { get; }
    public SettingsViewModel Settings { get; }

    public MainViewModel(
        AgendaViewModel agenda,
        SyncSettingsViewModel syncSettings,
        CronLauncherViewModel cronLauncher,
        SettingsViewModel settings,
        IGoogleCalendarService calendarService,
        IGoogleOAuthSyncService oauthSyncService)
    {
        Agenda = agenda;
        SyncSettings = syncSettings;
        CronLauncher = cronLauncher;
        Settings = settings;
        _calendarService = calendarService;
        _oauthSyncService = oauthSyncService;
    }

    [RelayCommand]
    public async System.Threading.Tasks.Task SyncAllNowAsync()
    {
        await Agenda.RefreshEventsAsync();
        if (_oauthSyncService.Settings.AutoSyncEnabled)
        {
            await SyncSettings.SyncNowAsync();
        }
    }
}
