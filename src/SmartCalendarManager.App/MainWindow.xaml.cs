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
        InitializeComponent();
        App.LogTrace("MainWindow InitializeComponent completed");

        ShowPanelCommand = new RelayCommand(ShowPanel);
        HidePanelCommand = new RelayCommand(HidePanel);
        SyncNowCommand = new AsyncRelayCommand(async () =>
        {
            var mainVm = App.GetService<MainViewModel>();
            await mainVm.SyncAllNowAsync();
        });
        ExitCommand = new RelayCommand(ExitApplication);

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
        }
    }

    private void ShowPanel()
    {
        AppWindow.Show();
        AppWindow.MoveInZOrderAtTop();
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

