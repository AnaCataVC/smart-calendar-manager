using System;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SmartCalendarManager.Core.Models;
using SmartCalendarManager.Core.Services;

namespace SmartCalendarManager.Core.ViewModels;

public partial class CronLauncherViewModel : ObservableObject
{
    private readonly ICronSchedulerService _scheduler;
    private readonly IAppLauncherService _launcher;

    public ObservableCollection<CronLauncherRule> Rules { get; } = new();

    [ObservableProperty]
    private string _newRuleName = string.Empty;

    [ObservableProperty]
    private string _newRuleTarget = "slack://";

    [ObservableProperty]
    private TimeSpan _newRuleTime = new TimeSpan(9, 0, 0);

    [ObservableProperty]
    private bool _mon = true;
    [ObservableProperty]
    private bool _tue = true;
    [ObservableProperty]
    private bool _wed = true;
    [ObservableProperty]
    private bool _thu = true;
    [ObservableProperty]
    private bool _fri = true;
    [ObservableProperty]
    private bool _sat = false;
    [ObservableProperty]
    private bool _sun = false;

    public CronLauncherViewModel(
        ICronSchedulerService scheduler,
        IAppLauncherService launcher)
    {
        _scheduler = scheduler;
        _launcher = launcher;

        LoadRules();
    }

    public void LoadRules()
    {
        Rules.Clear();
        foreach (var r in _scheduler.Rules)
        {
            Rules.Add(r);
        }
    }

    [RelayCommand]
    public void AddRule()
    {
        if (string.IsNullOrWhiteSpace(NewRuleName) || string.IsNullOrWhiteSpace(NewRuleTarget))
        {
            return;
        }

        DayOfWeekFlags flags = DayOfWeekFlags.None;
        if (Mon) flags |= DayOfWeekFlags.Monday;
        if (Tue) flags |= DayOfWeekFlags.Tuesday;
        if (Wed) flags |= DayOfWeekFlags.Wednesday;
        if (Thu) flags |= DayOfWeekFlags.Thursday;
        if (Fri) flags |= DayOfWeekFlags.Friday;
        if (Sat) flags |= DayOfWeekFlags.Saturday;
        if (Sun) flags |= DayOfWeekFlags.Sunday;

        var rule = new CronLauncherRule
        {
            Name = NewRuleName.Trim(),
            Target = NewRuleTarget.Trim(),
            TimeOfDay = NewRuleTime,
            Days = flags,
            IsEnabled = true,
            ActionType = NewRuleTarget.StartsWith("http", StringComparison.OrdinalIgnoreCase)
                ? CronActionType.WebUrl
                : CronActionType.UriProtocol
        };

        _scheduler.AddOrUpdateRule(rule);
        LoadRules();

        NewRuleName = string.Empty;
    }

    [RelayCommand]
    public void RemoveRule(CronLauncherRule? rule)
    {
        if (rule != null)
        {
            _scheduler.RemoveRule(rule.Id);
            LoadRules();
        }
    }

    [RelayCommand]
    public void TestRunRule(CronLauncherRule? rule)
    {
        if (rule != null)
        {
            _launcher.LaunchTarget(rule.Target, rule.Arguments);
        }
    }

    [RelayCommand]
    public void ToggleRule(CronLauncherRule? rule)
    {
        if (rule != null)
        {
            _scheduler.ToggleRule(rule.Id, !rule.IsEnabled);
            LoadRules();
        }
    }
}
