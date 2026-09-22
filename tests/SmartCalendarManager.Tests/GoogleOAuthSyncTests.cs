using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Google.Apis.Util.Store;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using SmartCalendarManager.Core.Models;
using SmartCalendarManager.Core.Services;
using Xunit;

namespace SmartCalendarManager.Tests;

public class GoogleOAuthSyncTests
{
    [Fact]
    public void CalendarSyncSettings_InitializesWithDefaults()
    {
        var settings = new CalendarSyncSettings();
        Assert.Equal("primary", settings.PersonalCalendarId);
        Assert.Equal("🔒 Ocupada", settings.BlockEventTitle);
        Assert.True(settings.AutoSyncEnabled);
        Assert.Equal(15, settings.SyncIntervalMinutes);
        Assert.Empty(settings.EventMappings);
    }

    [Fact]
    public void DayOfWeekFlags_ConversionsAndChecks_WorkCorrectly()
    {
        Assert.Equal(DayOfWeekFlags.Monday, DayOfWeek.Monday.ToFlag());
        Assert.Equal(DayOfWeekFlags.Sunday, DayOfWeek.Sunday.ToFlag());

        var weekdays = DayOfWeekFlags.Weekdays;
        Assert.True(weekdays.HasDay(DayOfWeek.Monday));
        Assert.True(weekdays.HasDay(DayOfWeek.Friday));
        Assert.False(weekdays.HasDay(DayOfWeek.Saturday));
        Assert.False(weekdays.HasDay(DayOfWeek.Sunday));
    }

    [Fact]
    public void CronLauncherRule_Formatting_GeneratesFriendlyStrings()
    {
        var rule = new CronLauncherRule
        {
            Name = "Morning Standup",
            Days = DayOfWeekFlags.Weekdays,
            TimeOfDay = new TimeSpan(9, 30, 0),
            ActionType = CronActionType.UriProtocol,
            Target = "slack://"
        };

        Assert.Equal("Lun - Vie", rule.FormattedDays);
        Assert.Equal(DateTime.Today.Add(new TimeSpan(9, 30, 0)).ToString("h:mm tt"), rule.FormattedTime);

        rule.Days = DayOfWeekFlags.All;
        Assert.Equal("Todos los días", rule.FormattedDays);
    }

    [Fact]
    public async Task DpapiDataStore_StoreAndRetrieve_RoundtripsSuccessfully()
    {
        var store = new DpapiDataStore("TestTokens");
        string key = "test_key_" + Guid.NewGuid().ToString("N");
        var tokenData = new { AccessToken = "abc123xyz", ExpiresIn = 3600 };

        await store.StoreAsync(key, tokenData);
        var retrieved = await store.GetAsync<dynamic>(key);

        Assert.NotNull(retrieved);

        await store.DeleteAsync<dynamic>(key);
        var afterDelete = await store.GetAsync<dynamic>(key);
        Assert.Null(afterDelete);
    }
}
