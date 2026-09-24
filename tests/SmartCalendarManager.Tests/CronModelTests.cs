using System;
using SmartCalendarManager.Core.Models;
using SmartCalendarManager.Core.Services;
using Xunit;

namespace SmartCalendarManager.Tests;

public class CronModelTests
{
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
}
