using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace SmartCalendarManager.Core.Services;

public class CalendarSyncBackgroundService : BackgroundService
{
    private readonly IGoogleCalendarService _calendarService;
    private readonly IGoogleOAuthSyncService _oauthSyncService;
    private readonly ICronSchedulerService _cronScheduler;
    private readonly ILogger<CalendarSyncBackgroundService> _logger;

    public CalendarSyncBackgroundService(
        IGoogleCalendarService calendarService,
        IGoogleOAuthSyncService oauthSyncService,
        ICronSchedulerService cronScheduler,
        ILogger<CalendarSyncBackgroundService> logger)
    {
        _calendarService = calendarService;
        _oauthSyncService = oauthSyncService;
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

            if (_oauthSyncService.Settings.AutoSyncEnabled)
            {
                await _oauthSyncService.SynchronizeAvailabilityAsync(stoppingToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error during initial background sync.");
        }

        // Auto-refresh calendar feed every 30 minutes
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(30));

        while (!stoppingToken.IsCancellationRequested && await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                _logger.LogInformation("Running scheduled 30-min calendar auto-refresh...");
                if (_calendarService.IsConfigured)
                {
                    await _calendarService.GetTodayEventsAsync();
                }

                if (_oauthSyncService.Settings.AutoSyncEnabled)
                {
                    await _oauthSyncService.SynchronizeAvailabilityAsync(stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in scheduled calendar sync.");
            }
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _cronScheduler.Stop();
        await base.StopAsync(cancellationToken);
    }
}
