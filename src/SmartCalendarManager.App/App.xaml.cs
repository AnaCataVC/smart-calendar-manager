using System;
using System.IO;
using Google.Apis.Util.Store;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using SmartCalendarManager.Core.Services;
using SmartCalendarManager.Core.ViewModels;

namespace SmartCalendarManager;

public partial class App : Application
{
    public static Window Window { get; private set; } = null!;
    public static DispatcherQueue DispatcherQueue { get; private set; } = null!;
    public static nint WindowHandle => WinRT.Interop.WindowNative.GetWindowHandle(Window);

    private static IHost? _host;

    public App()
    {
        UnhandledException += (s, e) =>
        {
            LogCrash("XAML_UnhandledException", e.Exception, e.Message);
        };
        AppDomain.CurrentDomain.UnhandledException += (s, e) =>
        {
            LogCrash("AppDomain_UnhandledException", e.ExceptionObject as Exception, e.ExceptionObject?.ToString());
        };
        System.Threading.Tasks.TaskScheduler.UnobservedTaskException += (s, e) =>
        {
            LogCrash("TaskScheduler_UnobservedTaskException", e.Exception, e.Exception?.ToString());
        };

        LogTrace("App() constructor started");

        try
        {
            InitializeComponent();
            LogTrace("InitializeComponent() completed");

            _host = Host.CreateDefaultBuilder()
                .ConfigureServices((context, services) =>
                {
                    // Secure Storage & Core Services
                    services.AddSingleton<IDataStore, DpapiDataStore>();
                    services.AddSingleton<IAppLauncherService, AppLauncherService>();
                    services.AddSingleton<IGoogleCalendarService, GoogleCalendarService>();
                    services.AddSingleton<IGoogleOAuthSyncService, GoogleOAuthSyncService>();
                    services.AddSingleton<ICronSchedulerService, CronSchedulerService>();
                    services.AddSingleton<IUpdateService, UpdateService>();

                    // Hosted Background Service
                    services.AddHostedService<CalendarSyncBackgroundService>();

                    // ViewModels
                    services.AddSingleton<AgendaViewModel>();
                    services.AddSingleton<SyncSettingsViewModel>();
                    services.AddSingleton<CronLauncherViewModel>();
                    services.AddSingleton<SettingsViewModel>();
                    services.AddSingleton<MainViewModel>();
                })
                .Build();

            LogTrace("Host built successfully");
        }
        catch (Exception ex)
        {
            LogCrash("App_Constructor", ex, ex.Message);
            throw;
        }
    }

    public static T GetService<T>() where T : class
    {
        return _host!.Services.GetRequiredService<T>();
    }

    protected override async void OnLaunched(LaunchActivatedEventArgs args)
    {
        LogTrace("OnLaunched() started");
        try
        {
            // CRITICAL: Initialize DispatcherQueue before Window instantiation
            DispatcherQueue = DispatcherQueue.GetForCurrentThread();
            LogTrace("DispatcherQueue obtained");

            // Start hosted background services
            await _host!.StartAsync();
            LogTrace("Host background services started");

            Window = new MainWindow();
            LogTrace("MainWindow instantiated");

            Window.Activate();
            LogTrace("Window.Activate() executed");
        }
        catch (Exception ex)
        {
            LogCrash("OnLaunched", ex, ex.Message);
            throw;
        }
    }

    public static void LogTrace(string step)
    {
        try
        {
            var logDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SmartCalendarManager", "Logs");
            Directory.CreateDirectory(logDir);
            File.AppendAllText(Path.Combine(logDir, "startup_diagnostic.log"), $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {step}\n");
        }
        catch { }
    }

    public static void LogCrash(string source, Exception? ex, string? message)
    {
        try
        {
            var logDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SmartCalendarManager", "Logs");
            Directory.CreateDirectory(logDir);
            var content = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] CRASH in {source}: {message}\nException: {ex}\nStackTrace: {ex?.StackTrace}\nInnerException: {ex?.InnerException}\n\n";
            File.AppendAllText(Path.Combine(logDir, "startup_diagnostic.log"), content);
            File.AppendAllText(Path.Combine(logDir, "crash.log"), content);
        }
        catch { }
    }
}
