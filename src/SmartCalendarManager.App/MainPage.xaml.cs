using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SmartCalendarManager.Core.Helpers;
using SmartCalendarManager.Core.Models;
using SmartCalendarManager.Core.ViewModels;

namespace SmartCalendarManager;

public sealed partial class MainPage : Page
{
    public MainViewModel ViewModel => App.GetService<MainViewModel>();

    public MainPage()
    {
        InitializeComponent();
        Loaded += async (s, e) =>
        {
            await ViewModel.Agenda.RefreshEventsAsync();
        };
    }

    private void NavView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.SelectedItem is NavigationViewItem item && item.Tag is string tag)
        {
            AgendaSection.Visibility = tag == "Agenda" ? Visibility.Visible : Visibility.Collapsed;
            SyncSection.Visibility = tag == "Sync" ? Visibility.Visible : Visibility.Collapsed;
            CronSection.Visibility = tag == "Cron" ? Visibility.Visible : Visibility.Collapsed;
            SettingsSection.Visibility = tag == "Settings" ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    private void InfoBar_CloseButtonClick(InfoBar sender, object args)
    {
        ViewModel.Agenda.DismissBanner();
    }

    private void JoinMeeting_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string url && !string.IsNullOrWhiteSpace(url))
        {
            ProcessLaunchHelper.ShellExecute(url);
        }
    }

    private void TestRun_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is CronLauncherRule rule)
        {
            ViewModel.CronLauncher.TestRunRule(rule);
        }
    }

    private void DeleteRule_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is CronLauncherRule rule)
        {
            ViewModel.CronLauncher.RemoveRule(rule);
        }
    }
}

