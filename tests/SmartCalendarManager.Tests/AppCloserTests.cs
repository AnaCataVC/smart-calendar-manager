using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Moq.Protected;
using SmartCalendarManager.Core.Models;
using SmartCalendarManager.Core.Services;
using SmartCalendarManager.Core.ViewModels;
using Xunit;

namespace SmartCalendarManager.Tests;

public class AppCloserTests
{
    [Theory]
    [InlineData(null, 0)]
    [InlineData("", 0)]
    [InlineData("   ", 0)]
    [InlineData("Spotify", 1)]
    [InlineData("Spotify.exe", 1)]
    [InlineData("Spotify, Discord", 2)]
    [InlineData("Spotify; Discord; Steam", 3)]
    [InlineData("Spotify\nDiscord\r\nSteam", 3)]
    [InlineData("Spotify, spotify.exe, SPOTIFY", 1)]
    [InlineData("  Spotify.EXE , Discord  ; steam.exe  ", 3)]
    public void ParseProcessNames_VariousInputs_ShouldSanitizeAndDeduplicate(string? input, int expectedCount)
    {
        var mockController = new Mock<IProcessController>();
        var closer = new AppCloserService(mockController.Object, NullLogger<AppCloserService>.Instance);

        var result = closer.ParseProcessNames(input);

        Assert.Equal(expectedCount, result.Count);
        if (expectedCount == 1 && input != null && input.Contains("Spotify", StringComparison.OrdinalIgnoreCase))
        {
            Assert.Equal("Spotify", result[0], ignoreCase: true);
        }
    }

    [Fact]
    public void CloseConfiguredApps_CallsControllerForEveryUniqueApp()
    {
        var mockController = new Mock<IProcessController>();
        mockController.Setup(c => c.TerminateProcessesByName("Spotify", It.IsAny<int>())).Returns(2);
        mockController.Setup(c => c.TerminateProcessesByName("Discord", It.IsAny<int>())).Returns(1);

        var closer = new AppCloserService(mockController.Object, NullLogger<AppCloserService>.Instance);

        int totalClosed = closer.CloseConfiguredApps("Spotify.exe, Discord, Spotify");

        Assert.Equal(3, totalClosed);
        mockController.Verify(c => c.TerminateProcessesByName("Spotify", It.IsAny<int>()), Times.Once);
        mockController.Verify(c => c.TerminateProcessesByName("Discord", It.IsAny<int>()), Times.Once);
    }

    [Fact]
    public void CloseConfiguredApps_ControllerThrowsException_DoesNotCrashAndContinuesRemainingApps()
    {
        var mockController = new Mock<IProcessController>();
        mockController.Setup(c => c.TerminateProcessesByName("BrokenApp", It.IsAny<int>()))
            .Throws(new InvalidOperationException("Process access denied"));
        mockController.Setup(c => c.TerminateProcessesByName("Spotify", It.IsAny<int>())).Returns(1);

        var closer = new AppCloserService(mockController.Object, NullLogger<AppCloserService>.Instance);

        int totalClosed = closer.CloseConfiguredApps("BrokenApp, Spotify");

        Assert.Equal(1, totalClosed);
        mockController.Verify(c => c.TerminateProcessesByName("Spotify", It.IsAny<int>()), Times.Once);
    }

    [Fact]
    public void GetRunningConfiguredApps_ReturnsOnlyCurrentlyRunningApps()
    {
        var mockController = new Mock<IProcessController>();
        mockController.Setup(c => c.IsProcessRunning("Spotify")).Returns(true);
        mockController.Setup(c => c.IsProcessRunning("Discord")).Returns(false);
        mockController.Setup(c => c.IsProcessRunning("Steam")).Returns(true);

        var closer = new AppCloserService(mockController.Object, NullLogger<AppCloserService>.Instance);

        var running = closer.GetRunningConfiguredApps("Spotify, Discord, Steam");

        Assert.Equal(2, running.Count);
        Assert.Contains("Spotify", running);
        Assert.Contains("Steam", running);
        Assert.DoesNotContain("Discord", running);
    }

    [Fact]
    public void CalendarFilterSettings_ShouldCloseApps_RespectsSettingsAndEligibility()
    {
        var settings = new CalendarFilterSettings
        {
            CloseAppsBeforeMeetingEnabled = true,
            AppsToClose = "Spotify",
            RequireMeetingLink = true
        };

        var validMeeting = new CalendarEvent
        {
            Id = "m1",
            Title = "Sprint Review",
            StartTime = DateTime.Now.AddMinutes(30),
            EndTime = DateTime.Now.AddMinutes(60),
            MeetingLink = "https://meet.google.com/abc-xyz"
        };

        var meetingWithoutLink = new CalendarEvent
        {
            Id = "m2",
            Title = "Lunch with Team",
            StartTime = DateTime.Now.AddMinutes(30),
            EndTime = DateTime.Now.AddMinutes(60),
            MeetingLink = null
        };

        var declinedMeeting = new CalendarEvent
        {
            Id = "m3",
            Title = "Sprint Review",
            StartTime = DateTime.Now.AddMinutes(30),
            EndTime = DateTime.Now.AddMinutes(60),
            MeetingLink = "https://meet.google.com/abc-xyz",
            IsDeclined = true
        };

        Assert.True(settings.ShouldCloseApps(validMeeting));
        Assert.False(settings.ShouldCloseApps(meetingWithoutLink));
        Assert.False(settings.ShouldCloseApps(declinedMeeting));

        // When feature is disabled
        settings.CloseAppsBeforeMeetingEnabled = false;
        Assert.False(settings.ShouldCloseApps(validMeeting));

        // When app list is empty
        settings.CloseAppsBeforeMeetingEnabled = true;
        settings.AppsToClose = "   ";
        Assert.False(settings.ShouldCloseApps(validMeeting));
    }

    [Fact]
    public void SettingsViewModel_TestCloseAppsCommand_UpdatesStatusMessage()
    {
        var mockCalendar = new Mock<IGoogleCalendarService>();
        mockCalendar.Setup(c => c.Feeds).Returns(new List<CalendarFeed>());
        mockCalendar.Setup(c => c.FilterSettings).Returns(new CalendarFilterSettings());

        var mockUpdate = new Mock<IUpdateService>();
        var mockLauncher = new Mock<IAppLauncherService>();
        var agenda = new AgendaViewModel(mockCalendar.Object, mockLauncher.Object, disableSyncContext: true);

        var mockCloser = new Mock<IAppCloserService>();
        mockCloser.Setup(c => c.CloseConfiguredApps("Spotify, Discord")).Returns(2);

        var vm = new SettingsViewModel(
            mockCalendar.Object,
            mockUpdate.Object,
            mockLauncher.Object,
            agenda,
            startupService: null,
            appCloser: mockCloser.Object)
        {
            AppsToClose = "Spotify, Discord"
        };

        vm.TestCloseAppsCommand.Execute(null);

        Assert.Contains("2", vm.StatusMessage);
        mockCloser.Verify(c => c.CloseConfiguredApps("Spotify, Discord"), Times.Once);
    }

    [Fact]
    public async Task SettingsViewModel_SaveCalendarSettingsCommand_PersistsCloseAppsSettings()
    {
        CalendarFilterSettings? captured = null;
        var mockCalendar = new Mock<IGoogleCalendarService>();
        mockCalendar.Setup(c => c.Feeds).Returns(new List<CalendarFeed>());
        mockCalendar.Setup(c => c.FilterSettings).Returns(new CalendarFilterSettings());
        mockCalendar.Setup(c => c.UpdateFilterSettings(It.IsAny<CalendarFilterSettings>()))
            .Callback<CalendarFilterSettings>(s => captured = s);

        var mockUpdate = new Mock<IUpdateService>();
        var mockLauncher = new Mock<IAppLauncherService>();
        var agenda = new AgendaViewModel(mockCalendar.Object, mockLauncher.Object, disableSyncContext: true);

        var vm = new SettingsViewModel(
            mockCalendar.Object,
            mockUpdate.Object,
            mockLauncher.Object,
            agenda)
        {
            CloseAppsBeforeMeeting = true,
            AppsToClose = "Spotify, Steam",
            CloseAppsLeadMinutes = 2
        };

        await vm.SaveCalendarSettingsCommand.ExecuteAsync(null);

        Assert.NotNull(captured);
        Assert.True(captured.CloseAppsBeforeMeetingEnabled);
        Assert.Equal("Spotify, Steam", captured.AppsToClose);
        Assert.Equal(2, captured.CloseAppsLeadMinutes);
    }

    [Fact]
    public void GoogleCalendarService_TriggersAppClosure_WhenMeetingStartsWithinLeadTime()
    {
        var mockLauncher = new Mock<IAppLauncherService>();
        var mockCloser = new Mock<IAppCloserService>();

        using var service = new GoogleCalendarService(
            mockLauncher.Object,
            NullLogger<GoogleCalendarService>.Instance,
            mockCloser.Object,
            httpHandler: null);

        var filterSettings = new CalendarFilterSettings
        {
            CloseAppsBeforeMeetingEnabled = true,
            AppsToClose = "Spotify",
            CloseAppsLeadMinutes = 2,
            RequireMeetingLink = false
        };
        service.UpdateFilterSettings(filterSettings);

        // Meeting starting in 1 minute (within the 2-minute lead window)
        var meeting = new CalendarEvent
        {
            Id = "meeting-imminent",
            Title = "Standup",
            StartTime = DateTime.Now.AddMinutes(1),
            EndTime = DateTime.Now.AddMinutes(31),
            OpensGranola = true,
            ClosesApps = true
        };

        service.ScheduleMeetingAlerts(new[] { meeting });

        mockCloser.Verify(c => c.CloseConfiguredApps("Spotify"), Times.Once);
    }

    [Theory]
    [InlineData("CANCELLED")]
    [InlineData("RESCHEDULED")]
    [InlineData("DECLINED")]
    public async Task ScheduledAppCloserTimer_WhenEventCancelledRescheduledOrDeclinedBeforeFiring_AbortsAppClosure(string changeMode)
    {
        // iCal DTSTART has 1-second resolution (yyyyMMddTHHmmss), so add 2 seconds to ensure alert time is in the future.
        var start = DateTime.Now.AddMinutes(1).AddSeconds(2);
        var end = start.AddMinutes(30);
        const string feedUrl = "https://calendar.google.com/calendar/ical/cata%40work.com/private-closer/basic.ics";

        var feedsDict = new Dictionary<string, string>
        {
            [feedUrl] =
                "BEGIN:VCALENDAR\r\nVERSION:2.0\r\nBEGIN:VEVENT\r\n" +
                "UID:closer-1\r\nSUMMARY:Daily Standup\r\n" +
                $"DTSTART:{start:yyyyMMddTHHmmss}\r\nDTEND:{end:yyyyMMddTHHmmss}\r\n" +
                "LOCATION:https://meet.google.com/abc-defg-hij\r\n" +
                "ATTENDEE;PARTSTAT=ACCEPTED:mailto:cata@work.com\r\n" +
                "STATUS:CONFIRMED\r\nEND:VEVENT\r\nEND:VCALENDAR\r\n"
        };

        var mockLauncher = new Mock<IAppLauncherService>();
        var mockCloser = new Mock<IAppCloserService>();
        var handler = new TestHttpHandler(feedsDict);

        using var service = new GoogleCalendarService(
            mockLauncher.Object,
            NullLogger<GoogleCalendarService>.Instance,
            mockCloser.Object,
            handler);

        service.UpdateFilterSettings(new CalendarFilterSettings
        {
            CloseAppsBeforeMeetingEnabled = true,
            AppsToClose = "Spotify",
            CloseAppsLeadMinutes = 1,
            GranolaLeadMinutes = 10,
            RequireMeetingLink = true
        });

        service.SaveFeeds(new[] { new CalendarFeed { Name = "Work", Url = feedUrl, Kind = CalendarFeedKind.Work } });

        // 1. Initial sync schedules the timer ~2s in the future
        var initialEvents = await service.GetTodayEventsAsync();
        Assert.Single(initialEvents);
        Assert.True(initialEvents[0].ClosesApps);
        mockCloser.Verify(c => c.CloseConfiguredApps(It.IsAny<string>()), Times.Never);

        // 2. Before the timer fires, change the feed
        feedsDict[feedUrl] = changeMode switch
        {
            "CANCELLED" =>
                "BEGIN:VCALENDAR\r\nVERSION:2.0\r\nBEGIN:VEVENT\r\n" +
                "UID:closer-1\r\nSUMMARY:Daily Standup\r\n" +
                $"DTSTART:{start:yyyyMMddTHHmmss}\r\nDTEND:{end:yyyyMMddTHHmmss}\r\n" +
                "STATUS:CANCELLED\r\nEND:VEVENT\r\nEND:VCALENDAR\r\n",
            "RESCHEDULED" =>
                "BEGIN:VCALENDAR\r\nVERSION:2.0\r\nBEGIN:VEVENT\r\n" +
                "UID:closer-1\r\nSUMMARY:Daily Standup\r\n" +
                $"DTSTART:{start.AddHours(2):yyyyMMddTHHmmss}\r\nDTEND:{end.AddHours(2):yyyyMMddTHHmmss}\r\n" +
                "LOCATION:https://meet.google.com/abc-defg-hij\r\n" +
                "STATUS:CONFIRMED\r\nEND:VEVENT\r\nEND:VCALENDAR\r\n",
            _ =>
                "BEGIN:VCALENDAR\r\nVERSION:2.0\r\nBEGIN:VEVENT\r\n" +
                "UID:closer-1\r\nSUMMARY:Daily Standup\r\n" +
                $"DTSTART:{start:yyyyMMddTHHmmss}\r\nDTEND:{end:yyyyMMddTHHmmss}\r\n" +
                "LOCATION:https://meet.google.com/abc-defg-hij\r\n" +
                "ATTENDEE;PARTSTAT=DECLINED:mailto:cata@work.com\r\n" +
                "STATUS:CONFIRMED\r\nEND:VEVENT\r\nEND:VCALENDAR\r\n"
        };

        // Wait for timer to elapse and JIT live verification to run
        await Task.Delay(2300);

        // Verify that app closer was ABORTED and NOT called
        mockCloser.Verify(c => c.CloseConfiguredApps(It.IsAny<string>()), Times.Never);
    }

    private sealed class TestHttpHandler : HttpMessageHandler
    {
        private readonly Dictionary<string, string> _feeds;
        public TestHttpHandler(Dictionary<string, string> feeds) => _feeds = feeds;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var url = request.RequestUri?.ToString() ?? string.Empty;
            if (_feeds.TryGetValue(url, out var body))
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(body)
                });
            }
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
        }
    }
}
