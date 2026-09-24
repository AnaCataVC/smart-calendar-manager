using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace SmartCalendarManager.Core.Services;

public class CalendarSyncBackgroundService : BackgroundService
{
    // Keeps the pre-meeting Granola timers fresh when events change during the day.
    private static readonly TimeSpan RefreshInterval = TimeSpan.FromMinutes(15);

    private readonly IGoogleCalendarService _calendarService;
    private readonly ICronSchedulerService _cronScheduler;
    private readonly ILogger<CalendarSyncBackgroundService> _logger;

    public CalendarSyncBackgroundService(
        IGoogleCalendarService calendarService,
        ICronSchedulerService cronScheduler,
        ILogger<CalendarSyncBackgroundService> logger)
    {
        _calendarService = calendarService;
        _cronScheduler = cronScheduler;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Calendar background worker starting...");
        _cronScheduler.Start();

        // Perform initial fetch on start
        try
        {
            if (_calendarService.IsConfigured)
            {
                await _calendarService.GetTodayEventsAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error during initial calendar fetch.");
        }

        using var timer = new PeriodicTimer(RefreshInterval);

        while (!stoppingToken.IsCancellationRequested && await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                _logger.LogInformation("Running scheduled {Interval}-min calendar auto-refresh...", RefreshInterval.TotalMinutes);

                if (_calendarService.IsConfigured)
                {
                    await _calendarService.GetTodayEventsAsync();
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in scheduled calendar refresh.");
            }
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _cronScheduler.Stop();
        await base.StopAsync(cancellationToken);
    }
}
