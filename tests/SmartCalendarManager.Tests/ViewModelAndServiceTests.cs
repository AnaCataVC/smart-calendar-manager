using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using SmartCalendarManager.Core.Models;
using SmartCalendarManager.Core.Services;
using SmartCalendarManager.Core.ViewModels;
using Xunit;

namespace SmartCalendarManager.Tests;

public class ViewModelAndServiceTests
{
    [Fact]
    public async Task AgendaViewModel_RefreshEvents_PopulatesEventsAndStatus()
    {
        var mockCalendar = new Mock<IGoogleCalendarService>();
        var mockLauncher = new Mock<IAppLauncherService>();

        var testEvents = new List<CalendarEvent>
        {
            new() { Id = "1", Title = "Meeting 1", StartTime = DateTime.Today.AddHours(9), EndTime = DateTime.Today.AddHours(10), MeetingLink = "https://meet.google.com/abc" },
            new() { Id = "2", Title = "Meeting 2", StartTime = DateTime.Today.AddHours(11), EndTime = DateTime.Today.AddHours(12) }
        };

        mockCalendar.Setup(c => c.GetTodayEventsAsync())
            .ReturnsAsync(testEvents);

        var vm = new AgendaViewModel(mockCalendar.Object, mockLauncher.Object);

        await vm.RefreshEventsCommand.ExecuteAsync(null);

        Assert.Equal(2, vm.TodayEvents.Count);
        Assert.Contains("2 reuniones encontradas para hoy", vm.StatusMessage);
        Assert.False(vm.IsLoading);
    }

    [Fact]
    public async Task AgendaViewModel_RefreshEvents_UnconfiguredShowsGuidance()
    {
        var mockCalendar = new Mock<IGoogleCalendarService>();
        var mockLauncher = new Mock<IAppLauncherService>();

        mockCalendar.Setup(c => c.GetTodayEventsAsync())
            .ReturnsAsync(new List<CalendarEvent>());
        mockCalendar.SetupGet(c => c.IsConfigured).Returns(false);

        var vm = new AgendaViewModel(mockCalendar.Object, mockLauncher.Object);

        await vm.RefreshEventsCommand.ExecuteAsync(null);

        Assert.Empty(vm.TodayEvents);
        Assert.Contains("No hay calendarios configurados", vm.StatusMessage);
    }

    [Fact]
    public void AgendaViewModel_JoinMeeting_LaunchesUrl()
    {
        var mockCalendar = new Mock<IGoogleCalendarService>();
        var mockLauncher = new Mock<IAppLauncherService>();

        var vm = new AgendaViewModel(mockCalendar.Object, mockLauncher.Object);
        var ev = new CalendarEvent { MeetingLink = "https://zoom.us/j/12345" };

        vm.JoinMeetingCommand.Execute(ev);

        mockLauncher.Verify(l => l.OpenUrl("https://zoom.us/j/12345"), Times.Once);
    }

    [Fact]
    public void AgendaViewModel_DismissBanner_DismissesAndHides()
    {
        var mockCalendar = new Mock<IGoogleCalendarService>();
        var mockLauncher = new Mock<IAppLauncherService>();
        mockCalendar.Setup(c => c.DismissedAlertIds).Returns(new HashSet<string>());

        // Disable SynchronizationContext so event runs inline synchronously during test
        var vm = new AgendaViewModel(mockCalendar.Object, mockLauncher.Object, disableSyncContext: true);
        var ev = new CalendarEvent { Id = "alert-1", Title = "Standup" };

        mockCalendar.Raise(c => c.UpcomingMeetingDetected += null, null, ev);

        Assert.True(vm.IsBannerVisible);
        Assert.Equal("alert-1", vm.ActiveBannerEvent?.Id);

        vm.DismissBannerCommand.Execute(null);

        mockCalendar.Verify(c => c.DismissAlert("alert-1"), Times.Once);
        Assert.False(vm.IsBannerVisible);
        Assert.Null(vm.ActiveBannerEvent);
    }

    [Fact]
    public void CronLauncherViewModel_AddAndToggleRule_InteractsWithScheduler()
    {
        var mockScheduler = new Mock<ICronSchedulerService>();
        var mockLauncher = new Mock<IAppLauncherService>();

        var rulesList = new List<CronLauncherRule>();
        mockScheduler.SetupGet(s => s.Rules).Returns(rulesList);
        mockScheduler.Setup(s => s.AddOrUpdateRule(It.IsAny<CronLauncherRule>()))
            .Callback<CronLauncherRule>(r => rulesList.Add(r));

        var vm = new CronLauncherViewModel(mockScheduler.Object, mockLauncher.Object)
        {
            NewRuleName = "Open Teams",
            NewRuleTarget = "msteams://",
            Mon = true,
            Fri = true
        };

        vm.AddRuleCommand.Execute(null);

        mockScheduler.Verify(s => s.AddOrUpdateRule(It.Is<CronLauncherRule>(r => r.Name == "Open Teams")), Times.Once);

        var rule = new CronLauncherRule { Id = "rule-1", Name = "Open Teams", IsEnabled = true };
        vm.ToggleRuleCommand.Execute(rule);

        mockScheduler.Verify(s => s.ToggleRule("rule-1", false), Times.Once);
    }

    [Fact]
    public void CronLauncherViewModel_TestRunRule_InvokesLauncherTarget()
    {
        var mockScheduler = new Mock<ICronSchedulerService>();
        var mockLauncher = new Mock<IAppLauncherService>();
        mockScheduler.SetupGet(s => s.Rules).Returns(new List<CronLauncherRule>());

        var vm = new CronLauncherViewModel(mockScheduler.Object, mockLauncher.Object);
        var rule = new CronLauncherRule { Target = "slack://", Arguments = "--workspace test" };

        vm.TestRunRuleCommand.Execute(rule);

        mockLauncher.Verify(l => l.LaunchTarget("slack://", "--workspace test"), Times.Once);
    }

    [Fact]
    public void UpdateService_NormalizeVersionString_StripsPrefix()
    {
        Assert.Equal("1.2.3", UpdateService.NormalizeVersionString("v1.2.3"));
        Assert.Equal("2.0.0", UpdateService.NormalizeVersionString("V2.0.0"));
        Assert.Equal("1.0.0", UpdateService.NormalizeVersionString("1.0.0"));
        Assert.Equal("0.0.0", UpdateService.NormalizeVersionString(string.Empty));
    }

    [Theory]
    [InlineData("1.0.0", "1.0.1", true)]
    [InlineData("1.0.0", "1.1.0", true)]
    [InlineData("1.0.0", "2.0.0", true)]
    [InlineData("1.0.1", "1.0.0", false)]
    [InlineData("1.0.0", "1.0.0", false)]
    [InlineData("v1.0.0", "v1.0.1", true)]
    public void UpdateService_IsNewerVersion_EvaluatesCorrectly(string current, string latest, bool expected)
    {
        Assert.Equal(expected, UpdateService.IsNewerVersion(current, latest));
    }

    [Fact]
    public async Task SettingsViewModel_CheckForUpdates_HandlesUpdateAvailable()
    {
        var mockCalendar = new Mock<IGoogleCalendarService>();
        var mockUpdate = new Mock<IUpdateService>();

        mockCalendar.SetupGet(c => c.FilterSettings).Returns(new CalendarFilterSettings());
        mockCalendar.SetupGet(c => c.Feeds).Returns(new List<CalendarFeed>());
        mockUpdate.SetupGet(u => u.CurrentAppVersion).Returns("1.0.0");
        mockUpdate.Setup(u => u.CheckForUpdatesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UpdateInfo
            {
                CurrentVersion = "1.0.0",
                LatestVersion = "1.1.0",
                IsUpdateAvailable = true,
                DownloadUrl = "https://github.com/AnaCataVC/smart-calendar-manager/releases/download/v1.1.0/setup.exe"
            });

        var launcher = new Mock<IAppLauncherService>().Object;
        var vm = new SettingsViewModel(mockCalendar.Object, mockUpdate.Object, launcher, new AgendaViewModel(mockCalendar.Object, launcher));

        await vm.CheckForUpdatesCommand.ExecuteAsync(null);

        Assert.True(vm.IsUpdateAvailable);
        Assert.Contains("1.1.0", vm.UpdateStatusText);
        Assert.NotNull(vm.AvailableUpdate);
    }

    [Fact]
    public void MainViewModel_InitializesAllSubViewModels()
    {
        var mockCalendar = new Mock<IGoogleCalendarService>();
        var mockLauncher = new Mock<IAppLauncherService>();
        var mockScheduler = new Mock<ICronSchedulerService>();
        var mockUpdate = new Mock<IUpdateService>();

        mockCalendar.SetupGet(c => c.FilterSettings).Returns(new CalendarFilterSettings());
        mockCalendar.SetupGet(c => c.Feeds).Returns(new List<CalendarFeed>());
        mockScheduler.SetupGet(s => s.Rules).Returns(new List<CronLauncherRule>());
        mockUpdate.SetupGet(u => u.CurrentAppVersion).Returns("1.0.0");

        var agendaVm = new AgendaViewModel(mockCalendar.Object, mockLauncher.Object);
        var cronVm = new CronLauncherViewModel(mockScheduler.Object, mockLauncher.Object);
        var settingsVm = new SettingsViewModel(mockCalendar.Object, mockUpdate.Object, mockLauncher.Object, agendaVm);

        var mainVm = new MainViewModel(agendaVm, cronVm, settingsVm);

        Assert.NotNull(mainVm.Agenda);
        Assert.NotNull(mainVm.CronLauncher);
        Assert.NotNull(mainVm.Settings);
    }
}
