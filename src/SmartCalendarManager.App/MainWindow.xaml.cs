using System;
using System.IO;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml;

using SmartCalendarManager.Core.ViewModels;

namespace SmartCalendarManager;

public sealed partial class MainWindow : Window
{
    private bool _isExplicitExit;

    public IRelayCommand ShowPanelCommand { get; }
    public IRelayCommand HidePanelCommand { get; }
    public IRelayCommand SyncNowCommand { get; }
    public IRelayCommand ExitCommand { get; }

    public MainWindow()
    {
        App.LogTrace("MainWindow constructor started");

        ShowPanelCommand = new RelayCommand(ShowPanel);
        HidePanelCommand = new RelayCommand(HidePanel);
        SyncNowCommand = new AsyncRelayCommand(async () =>
        {
            var mainVm = App.GetService<MainViewModel>();
            await mainVm.Agenda.RefreshEventsAsync();
        });
        ExitCommand = new RelayCommand(ExitApplication);

        InitializeComponent();
        App.LogTrace("MainWindow InitializeComponent completed");

        // Force early bindings initialization even when started minimized without Window.Activate()
        Bindings.Initialize();
        App.LogTrace("MainWindow Bindings initialized");

        // Explicit command and click bindings for reliable Win32 native tray interop
        OpenMenuItem.Command = ShowPanelCommand;
        OpenMenuItem.Click += (s, e) => ShowPanel();

        SyncMenuItem.Command = SyncNowCommand;
        SyncMenuItem.Click += async (s, e) =>
        {
            var mainVm = App.GetService<MainViewModel>();
            await mainVm.Agenda.RefreshEventsAsync();
        };

        MinimizeMenuItem.Command = HidePanelCommand;
        MinimizeMenuItem.Click += (s, e) => HidePanel();

        ExitMenuItem.Command = ExitCommand;
        ExitMenuItem.Click += (s, e) => ExitApplication();

        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);
        App.LogTrace("MainWindow TitleBar configured");

        var iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "AppIcon.ico");
        if (File.Exists(iconPath))
        {
            AppWindow.SetIcon(iconPath);
            App.LogTrace("MainWindow AppIcon set");
        }

        AppWindow.Closing += (sender, args) =>
        {
            if (!_isExplicitExit)
            {
                args.Cancel = true;
                AppWindow.Hide();
            }
        };

        TrayIcon.LeftClickCommand = new RelayCommand(ToggleWindowVisibility);
        App.LogTrace("MainWindow TrayIcon configured");

        RootFrame.Navigate(typeof(MainPage));
        App.LogTrace("MainWindow navigation to MainPage completed");
    }

    private void ToggleWindowVisibility()
    {
        if (AppWindow.IsVisible)
        {
            AppWindow.Hide();
        }
        else
        {
            AppWindow.Show();
            AppWindow.MoveInZOrderAtTop();
            Activate();
        }
    }

    private void ShowPanel()
    {
        AppWindow.Show();
        AppWindow.MoveInZOrderAtTop();
        Activate();
    }

    private void HidePanel()
    {
        AppWindow.Hide();
    }

    private void ExitApplication()
    {
        _isExplicitExit = true;
        TrayIcon.Dispose();
        Application.Current.Exit();
        Environment.Exit(0);
    }
}

