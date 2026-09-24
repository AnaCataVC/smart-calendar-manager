using CommunityToolkit.Mvvm.ComponentModel;

namespace SmartCalendarManager.Core.ViewModels;

public partial class MainViewModel : ObservableObject
{
    [ObservableProperty]
    private string _title = "Smart Calendar Manager";

    public AgendaViewModel Agenda { get; }
    public CronLauncherViewModel CronLauncher { get; }
    public SettingsViewModel Settings { get; }

    public MainViewModel(
        AgendaViewModel agenda,
        CronLauncherViewModel cronLauncher,
        SettingsViewModel settings)
    {
        Agenda = agenda;
        CronLauncher = cronLauncher;
        Settings = settings;
    }
}
