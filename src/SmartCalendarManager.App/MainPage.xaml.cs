using Windows.ApplicationModel.DataTransfer;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using SmartCalendarManager.Core.Models;
using SmartCalendarManager.Core.ViewModels;

namespace SmartCalendarManager;

public sealed partial class MainPage : Page
{
    // Cache singleton reference to avoid repeated DI lookups on every property access.
    private readonly MainViewModel _viewModel = App.GetService<MainViewModel>();

    public MainViewModel ViewModel => _viewModel;

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
        var tag = args.IsSettingsSelected
            ? "Settings"
            : (args.SelectedItem as NavigationViewItem)?.Tag as string;
        if (tag == null) return;

        AgendaSection.Visibility = tag == "Agenda" ? Visibility.Visible : Visibility.Collapsed;
        CronSection.Visibility = tag == "Cron" ? Visibility.Visible : Visibility.Collapsed;
        SettingsSection.Visibility = tag == "Settings" ? Visibility.Visible : Visibility.Collapsed;
    }

    private void InfoBar_CloseButtonClick(InfoBar sender, object args)
    {
        ViewModel.Agenda.DismissBanner();
    }

    // Routes join-meeting through the ViewModel command instead of calling ProcessLaunchHelper directly.
    private void JoinMeeting_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is CalendarEvent ev)
        {
            ViewModel.Agenda.JoinMeetingCommand.Execute(ev);
        }
    }

    private void OpenGranola_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is CalendarEvent ev)
        {
            ViewModel.Agenda.OpenGranolaCommand.Execute(ev);
        }
    }

    private void DeleteFeed_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is CalendarFeed feed)
        {
            ViewModel.Settings.RemoveFeedCommand.Execute(feed);
        }
    }

    private void CopyScript_Click(object sender, RoutedEventArgs e)
    {
        var package = new DataPackage();
        package.SetText(ViewModel.Settings.GeneratedScript);
        Clipboard.SetContent(package);
    }

    // Routes test-run through the generated RelayCommand on CronLauncherViewModel.
    private void TestRun_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is CronLauncherRule rule)
        {
            ViewModel.CronLauncher.TestRunRuleCommand.Execute(rule);
        }
    }

    // Routes delete through the generated RelayCommand on CronLauncherViewModel.
    private void DeleteRule_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is CronLauncherRule rule)
        {
            ViewModel.CronLauncher.RemoveRuleCommand.Execute(rule);
        }
    }

    // Routes enable/disable toggle through CronSchedulerService via ViewModel.
    private void ToggleRule_Click(object sender, RoutedEventArgs e)
    {
        if (sender is ToggleButton btn && btn.Tag is CronLauncherRule rule)
        {
            ViewModel.CronLauncher.ToggleRuleCommand.Execute(rule);
        }
    }
}
