using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using SmartCalendarManager.Core.Helpers;
using SmartCalendarManager.Core.Models;
using SmartCalendarManager.Core.Services;
using SmartCalendarManager.Core.ViewModels;
using Xunit;

namespace SmartCalendarManager.Tests;

public class CalendarFeedTests : IDisposable
{
    private readonly string _settingsPath = Path.Combine(Path.GetTempPath(), $"scm-settings-{Guid.NewGuid():N}.json");

    public CalendarFeedTests()
    {
        LocalSettingsHelper.SettingsFilePath = _settingsPath;
    }

    public void Dispose()
    {
        LocalSettingsHelper.ResetToDefaultPath();
        if (File.Exists(_settingsPath)) File.Delete(_settingsPath);
    }

    private sealed class FakeFeedHandler : HttpMessageHandler
    {
        private readonly Dictionary<string, string> _feeds;

        public Dictionary<string, string?> AuthByUrl { get; } = new();

        public FakeFeedHandler(Dictionary<string, string> feeds) => _feeds = feeds;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            AuthByUrl[request.RequestUri!.ToString()] = request.Headers.Authorization?.ToString();
            var response = _feeds.TryGetValue(request.RequestUri!.ToString(), out var ics)
                ? new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(ics) }
                : new HttpResponseMessage(HttpStatusCode.InternalServerError);
            return Task.FromResult(response);
        }
    }

    private static string IcsWithEventAt(string uid, string title, int hour) =>
        "BEGIN:VCALENDAR\r\nVERSION:2.0\r\nBEGIN:VEVENT\r\n" +
        $"UID:{uid}\r\nSUMMARY:{title}\r\n" +
        $"DTSTART:{DateTime.Today.AddHours(hour):yyyyMMddTHHmmss}\r\n" +
        $"DTEND:{DateTime.Today.AddHours(hour + 1):yyyyMMddTHHmmss}\r\n" +
        "END:VEVENT\r\nEND:VCALENDAR\r\n";

    private static GoogleCalendarService CreateService(HttpMessageHandler? handler = null) =>
        new(new Mock<IAppLauncherService>().Object, NullLogger<GoogleCalendarService>.Instance, handler);

    [Fact]
    public void LegacySingleICalUrl_IsMigratedIntoOneWorkFeed()
    {
        LocalSettingsHelper.Set("CalendarICalUrl", "https://example.com/work.ics");

        var service = CreateService();

        var feed = Assert.Single(service.Feeds);
        Assert.Equal("https://example.com/work.ics", feed.Url);
        Assert.Equal(CalendarFeedKind.Work, feed.Kind);
        Assert.True(feed.Enabled);
        Assert.Null(LocalSettingsHelper.Get("CalendarICalUrl"));
        Assert.Single(LocalSettingsHelper.LoadJson<List<CalendarFeed>>("CalendarFeeds")!);
    }

    [Fact]
    public async Task LegacyBasicAuthKey_IsMigratedAndSentOnlyForThatFeed()
    {
        LocalSettingsHelper.Set("CalendarICalUrl", "https://example.com/work.ics");
        LocalSettingsHelper.Set("CalendarICalKey", "s3cret");
        var handler = new FakeFeedHandler(new Dictionary<string, string>
        {
            ["https://example.com/work.ics"] = IcsWithEventAt("w1", "Work sync", 11),
            ["https://example.com/personal.ics"] = IcsWithEventAt("p1", "Dentist", 9)
        });
        var service = CreateService(handler);

        Assert.Equal("s3cret", Assert.Single(service.Feeds).AuthKey);
        Assert.Null(LocalSettingsHelper.Get("CalendarICalKey"));

        service.SaveFeeds(service.Feeds.Append(new CalendarFeed { Name = "Gmail", Url = "https://example.com/personal.ics", Kind = CalendarFeedKind.Personal }));
        await service.GetTodayEventsAsync();

        var expected = "Basic " + Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("calendar:s3cret"));
        Assert.Equal(expected, handler.AuthByUrl["https://example.com/work.ics"]);
        Assert.Null(handler.AuthByUrl["https://example.com/personal.ics"]);
    }

    [Fact]
    public async Task SettingsViewModel_SaveAndRemoveFeed_RefreshAgendaSoGranolaTimersAreRescheduled()
    {
        var calendar = new Mock<IGoogleCalendarService>();
        var launcher = new Mock<IAppLauncherService>();
        var feed = new CalendarFeed { Name = "Gmail", Url = "https://example.com/personal.ics", Kind = CalendarFeedKind.Personal };
        calendar.SetupGet(c => c.Feeds).Returns(new List<CalendarFeed> { feed });
        calendar.SetupGet(c => c.FilterSettings).Returns(new CalendarFilterSettings());
        calendar.Setup(c => c.GetTodayEventsAsync()).ReturnsAsync(new List<CalendarEvent>());
        var agenda = new AgendaViewModel(calendar.Object, launcher.Object, disableSyncContext: true);
        var vm = new SettingsViewModel(calendar.Object, new Mock<IUpdateService>().Object, launcher.Object, agenda);

        await vm.SaveCalendarSettingsCommand.ExecuteAsync(null);
        calendar.Verify(c => c.GetTodayEventsAsync(), Times.Once);

        await vm.RemoveFeedCommand.ExecuteAsync(feed);
        calendar.Verify(c => c.GetTodayEventsAsync(), Times.Exactly(2));
        Assert.Empty(vm.Feeds);
        calendar.Verify(c => c.SaveFeeds(It.IsAny<IEnumerable<CalendarFeed>>()), Times.Exactly(2));
    }

    [Fact]
    public void AppsScriptGenerator_AbortsOnNonCalendarResponse()
    {
        var script = AppsScriptGenerator.Build(Array.Empty<CalendarFeed>(), "x");
        var guard = script.IndexOf("BEGIN:VCALENDAR", StringComparison.Ordinal);
        Assert.True(guard >= 0 && guard < script.IndexOf("deleteEvent", StringComparison.Ordinal));
    }

    [Fact]
    public async Task GetTodayEvents_MergesFeedsSortedAndSurvivesAFailingFeed()
    {
        var handler = new FakeFeedHandler(new Dictionary<string, string>
        {
            ["https://example.com/work.ics"] = IcsWithEventAt("w1", "Work sync", 11),
            ["https://example.com/personal.ics"] = IcsWithEventAt("p1", "Dentist", 9)
        });
        var service = CreateService(handler);
        service.SaveFeeds(new[]
        {
            new CalendarFeed { Name = "Trabajo", Url = "https://example.com/work.ics", Kind = CalendarFeedKind.Work },
            new CalendarFeed { Name = "Gmail", Url = "https://example.com/personal.ics", Kind = CalendarFeedKind.Personal },
            new CalendarFeed { Name = "Roto", Url = "https://example.com/broken.ics", Kind = CalendarFeedKind.Personal },
            new CalendarFeed { Name = "Apagado", Url = "https://example.com/disabled.ics", Enabled = false }
        });

        var events = await service.GetTodayEventsAsync();

        Assert.Equal(new[] { "Dentist", "Work sync" }, events.Select(e => e.Title));
        Assert.Equal(CalendarFeedKind.Personal, events[0].FeedKind);
        Assert.Equal("Gmail", events[0].FeedName);
        Assert.Equal(new[] { "Roto" }, service.FailedFeedNames);
    }

    [Fact]
    public async Task GetTodayEvents_GranolaScopeWorkOnly_ExcludesPersonalEvents()
    {
        var handler = new FakeFeedHandler(new Dictionary<string, string>
        {
            ["https://example.com/work.ics"] = IcsWithEventAt("w1", "Work sync", 11),
            ["https://example.com/personal.ics"] = IcsWithEventAt("p1", "Dentist", 9)
        });
        var service = CreateService(handler);
        service.SaveFeeds(new[]
        {
            new CalendarFeed { Name = "Trabajo", Url = "https://example.com/work.ics", Kind = CalendarFeedKind.Work },
            new CalendarFeed { Name = "Gmail", Url = "https://example.com/personal.ics", Kind = CalendarFeedKind.Personal }
        });

        service.UpdateFilterSettings(new CalendarFilterSettings { GranolaScope = GranolaScope.WorkOnly });
        var workOnly = await service.GetTodayEventsAsync();
        Assert.False(workOnly.Single(e => e.Title == "Dentist").InGranolaScope);
        Assert.False(workOnly.Single(e => e.Title == "Dentist").OpensGranola);
        Assert.True(workOnly.Single(e => e.Title == "Work sync").OpensGranola);

        service.UpdateFilterSettings(new CalendarFilterSettings { GranolaScope = GranolaScope.All });
        var all = await service.GetTodayEventsAsync();
        Assert.All(all, e => Assert.True(e.InGranolaScope && e.OpensGranola));
    }

    [Fact]
    public void AppsScriptGenerator_EmbedsEveryPersonalUrlAndTitle()
    {
        var feeds = new[]
        {
            new CalendarFeed { Url = "https://calendar.google.com/calendar/ical/a%40gmail.com/private-1/basic.ics", Kind = CalendarFeedKind.Personal },
            new CalendarFeed { Url = "https://example.com/family.ics?token=x&y=1", Kind = CalendarFeedKind.Personal }
        };

        var script = AppsScriptGenerator.Build(feeds, "🔒 Ocupada");

        foreach (var feed in feeds)
        {
            Assert.Contains($"\"{feed.Url}\"", script);
        }
        // Non-ASCII is emitted as \u escapes, which JavaScript decodes back to the same title.
        Assert.Contains("const BLOCK_TITLE = \"\\uD83D\\uDD12 Ocupada\";", script);
        Assert.DoesNotContain("\r", script);
        Assert.Contains("function syncBlocks()", script);
    }

    [Fact]
    public void AgendaViewModel_OpenGranola_LaunchesGranolaAndMeetingLink()
    {
        var launcher = new Mock<IAppLauncherService>();
        var vm = new AgendaViewModel(new Mock<IGoogleCalendarService>().Object, launcher.Object, disableSyncContext: true);

        vm.OpenGranolaCommand.Execute(new CalendarEvent { MeetingLink = "https://meet.google.com/abc" });
        vm.OpenGranolaCommand.Execute(new CalendarEvent());

        launcher.Verify(l => l.LaunchGranola(), Times.Exactly(2));
        launcher.Verify(l => l.OpenUrl("https://meet.google.com/abc"), Times.Once);
        launcher.Verify(l => l.OpenUrl(It.IsAny<string?>()), Times.Once);
    }
}
