using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Microsoft.Extensions.Logging;
using SmartCalendarManager.Core.Helpers;
using SmartCalendarManager.Core.Models;

namespace SmartCalendarManager.Core.Services;

public interface ICronSchedulerService : IDisposable
{
    IReadOnlyList<CronLauncherRule> Rules { get; }
    void Start();
    void Stop();
    void AddOrUpdateRule(CronLauncherRule rule);
    void RemoveRule(string ruleId);
    void ToggleRule(string ruleId, bool isEnabled);
}

public class CronSchedulerService : ICronSchedulerService
{
    private const string RulesStorageKey = "CronLauncherRules";
    private readonly IAppLauncherService _appLauncherService;
    private readonly ILogger<CronSchedulerService> _logger;
    private readonly List<CronLauncherRule> _rules = new();
    private Timer? _heartbeatTimer;
    private int _lastTriggeredMinute = -1;

    public IReadOnlyList<CronLauncherRule> Rules => _rules.AsReadOnly();

    public CronSchedulerService(
        IAppLauncherService appLauncherService,
        ILogger<CronSchedulerService> logger)
    {
        _appLauncherService = appLauncherService;
        _logger = logger;

        LoadRules();
    }

    private void LoadRules()
    {
        var saved = LocalSettingsHelper.LoadJson<List<CronLauncherRule>>(RulesStorageKey);
        if (saved != null && saved.Count > 0)
        {
            _rules.Clear();
            _rules.AddRange(saved);
        }
        else
        {
            // Seed default friendly rule
            _rules.Add(new CronLauncherRule
            {
                Name = "Abrir Slack laboral",
                Days = DayOfWeekFlags.Weekdays,
                TimeOfDay = new TimeSpan(9, 0, 0),
                ActionType = CronActionType.UriProtocol,
                Target = "slack://"
            });
            SaveRules();
        }
    }

    private void SaveRules()
    {
        LocalSettingsHelper.SaveJson(RulesStorageKey, _rules);
    }

    public void Start()
    {
        Stop();
        _logger.LogInformation("Starting CronSchedulerService heartbeat timer...");
        // Check every 10 seconds for precise minute matching without drift
        _heartbeatTimer = new Timer(OnHeartbeatTick, null, TimeSpan.Zero, TimeSpan.FromSeconds(10));
    }

    public void Stop()
    {
        _heartbeatTimer?.Dispose();
        _heartbeatTimer = null;
        _logger.LogInformation("Stopped CronSchedulerService heartbeat timer.");
    }

    private void OnHeartbeatTick(object? state)
    {
        var now = DateTime.Now;
        int currentMinuteOfDay = now.Hour * 60 + now.Minute;

        if (currentMinuteOfDay == _lastTriggeredMinute)
        {
            return; // Already evaluated for this current minute
        }

        _lastTriggeredMinute = currentMinuteOfDay;
        var todayFlag = now.DayOfWeek.ToFlag();

        foreach (var rule in _rules.Where(r => r.IsEnabled))
        {
            if (rule.Days.HasFlag(todayFlag))
            {
                if (rule.TimeOfDay.Hours == now.Hour && rule.TimeOfDay.Minutes == now.Minute)
                {
                    ExecuteRule(rule);
                }
            }
        }
    }

    private void ExecuteRule(CronLauncherRule rule)
    {
        _logger.LogInformation("Executing cron rule '{Name}' -> {Target}", rule.Name, rule.Target);

        switch (rule.ActionType)
        {
            case CronActionType.UriProtocol:
            case CronActionType.Executable:
                _appLauncherService.LaunchTarget(rule.Target, rule.Arguments);
                break;
            case CronActionType.WebUrl:
                _appLauncherService.OpenUrl(rule.Target);
                break;
        }
    }

    public void AddOrUpdateRule(CronLauncherRule rule)
    {
        var existing = _rules.FirstOrDefault(r => r.Id == rule.Id);
        if (existing != null)
        {
            _rules.Remove(existing);
        }

        _rules.Add(rule);
        SaveRules();
        _logger.LogInformation("Rule '{Name}' added/updated.", rule.Name);
    }

    public void RemoveRule(string ruleId)
    {
        var removed = _rules.RemoveAll(r => r.Id == ruleId);
        if (removed > 0)
        {
            SaveRules();
            _logger.LogInformation("Rule {RuleId} removed.", ruleId);
        }
    }

    public void ToggleRule(string ruleId, bool isEnabled)
    {
        var rule = _rules.FirstOrDefault(r => r.Id == ruleId);
        if (rule != null)
        {
            rule.IsEnabled = isEnabled;
            SaveRules();
            _logger.LogInformation("Rule '{Name}' toggled: {IsEnabled}", rule.Name, isEnabled);
        }
    }

    public void Dispose()
    {
        Stop();
    }
}
