using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SmartCalendarManager.Core.Models;
using SmartCalendarManager.Core.Services;

namespace SmartCalendarManager.Core.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private const string AppsScriptEditorUrl = "https://script.google.com/home/projects/create";

    private readonly IGoogleCalendarService _calendarService;
    private readonly IUpdateService _updateService;
    private readonly IAppLauncherService _appLauncher;
    private readonly AgendaViewModel _agenda;

    public ObservableCollection<CalendarFeed> Feeds { get; } = new();

    [ObservableProperty]
    private string _newFeedName = string.Empty;

    [ObservableProperty]
    private string _newFeedUrl = string.Empty;

    [ObservableProperty]
    private string _newFeedKey = string.Empty;

    [ObservableProperty]
    private int _newFeedKindIndex = (int)CalendarFeedKind.Work;

    [ObservableProperty]
    private int _granolaScopeIndex = (int)GranolaScope.WorkOnly;

    [ObservableProperty]
    private double _granolaLeadMinutes = 5;

    [ObservableProperty]
    private string _blockTitle = "🔒 Ocupada";

    [ObservableProperty]
    private string _generatedScript = string.Empty;

    [ObservableProperty]
    private string _excludedKeywords = string.Empty;

    [ObservableProperty]
    private bool _ignoreAllDay = true;

    [ObservableProperty]
    private bool _requireLink = true;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string _currentVersion = "1.0.0";

    [ObservableProperty]
    private string _updateStatusText = string.Empty;

    [ObservableProperty]
    private bool _isUpdateAvailable;

    [ObservableProperty]
    private UpdateInfo? _availableUpdate;

    [ObservableProperty]
    private bool _isDownloading;

    [ObservableProperty]
    private string _downloadProgress = string.Empty;

    public SettingsViewModel(
        IGoogleCalendarService calendarService,
        IUpdateService updateService,
        IAppLauncherService appLauncher,
        AgendaViewModel agenda)
    {
        _calendarService = calendarService;
        _updateService = updateService;
        _appLauncher = appLauncher;
        _agenda = agenda;

        foreach (var feed in _calendarService.Feeds)
        {
            Feeds.Add(feed);
        }

        var f = _calendarService.FilterSettings;
        ExcludedKeywords = f.ExcludedKeywords;
        IgnoreAllDay = f.IgnoreAllDayEvents;
        RequireLink = f.RequireMeetingLink;
        GranolaScopeIndex = (int)f.GranolaScope;
        GranolaLeadMinutes = f.GranolaLeadMinutes;

        CurrentVersion = _updateService.CurrentAppVersion;
        RegenerateScript();
    }

    partial void OnBlockTitleChanged(string value) => RegenerateScript();

    private void RegenerateScript()
    {
        GeneratedScript = AppsScriptGenerator.Build(
            Feeds.Where(feed => feed.Kind == CalendarFeedKind.Personal && feed.Enabled),
            BlockTitle);
    }

    // Refetching through the agenda also reschedules the Granola timers, so removed feeds or a
    // narrower Granola scope stop firing immediately.
    private async Task PersistFeedsAndRefreshAsync()
    {
        _calendarService.SaveFeeds(Feeds);
        RegenerateScript();
        await _agenda.RefreshEventsAsync();
    }

    [RelayCommand]
    public async Task AddFeedAsync()
    {
        if (string.IsNullOrWhiteSpace(NewFeedUrl)) return;

        IsBusy = true;
        StatusMessage = "Verificando feed iCal...";
        try
        {
            var url = NewFeedUrl.Trim();
            var key = string.IsNullOrWhiteSpace(NewFeedKey) ? null : NewFeedKey.Trim();
            if (!await _calendarService.IsValidFeedAsync(url, key))
            {
                StatusMessage = "La URL no devolvió un calendario iCal válido. No se agregó.";
                return;
            }

            var kind = (CalendarFeedKind)NewFeedKindIndex;
            Feeds.Add(new CalendarFeed
            {
                Name = string.IsNullOrWhiteSpace(NewFeedName) ? CalendarFeed.LabelFor(kind) : NewFeedName.Trim(),
                Url = url,
                Kind = kind,
                AuthKey = key
            });
            await PersistFeedsAndRefreshAsync();

            NewFeedName = string.Empty;
            NewFeedUrl = string.Empty;
            NewFeedKey = string.Empty;
            StatusMessage = "Calendario agregado.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    public async Task RemoveFeedAsync(CalendarFeed? feed)
    {
        if (feed != null && Feeds.Remove(feed))
        {
            await PersistFeedsAndRefreshAsync();
        }
    }

    [RelayCommand]
    public void OpenScriptEditor() => _appLauncher.OpenUrl(AppsScriptEditorUrl);

    [RelayCommand]
    public async Task SaveCalendarSettingsAsync()
    {
        try
        {
            var f = new CalendarFilterSettings
            {
                ExcludedKeywords = ExcludedKeywords,
                IgnoreAllDayEvents = IgnoreAllDay,
                RequireMeetingLink = RequireLink,
                GranolaScope = (GranolaScope)GranolaScopeIndex,
                // NumberBox yields NaN when cleared; fall back to the default lead time.
                GranolaLeadMinutes = double.IsNaN(GranolaLeadMinutes) ? 5 : Math.Clamp((int)GranolaLeadMinutes, 0, 60)
            };
            _calendarService.UpdateFilterSettings(f);
            await PersistFeedsAndRefreshAsync();

            StatusMessage = "Configuración guardada con éxito.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error al guardar: {ex.Message}";
        }
    }

    [RelayCommand]
    public async Task CheckForUpdatesAsync()
    {
        IsBusy = true;
        UpdateStatusText = "Buscando actualizaciones...";
        try
        {
            var info = await _updateService.CheckForUpdatesAsync();
            if (info.IsUpdateAvailable)
            {
                IsUpdateAvailable = true;
                AvailableUpdate = info;
                UpdateStatusText = $"¡Nueva versión {info.LatestVersion} disponible!";
            }
            else
            {
                IsUpdateAvailable = false;
                UpdateStatusText = "Ya tienes la última versión instalada.";
            }
        }
        catch (Exception ex)
        {
            UpdateStatusText = $"Error al buscar: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanDownloadUpdate))]
    public async Task DownloadAndInstallUpdateAsync()
    {
        if (AvailableUpdate == null) return;
        IsDownloading = true;
        IsBusy = true;
        UpdateStatusText = "Iniciando descarga...";
        try
        {
            var progress = new Progress<double>(p =>
                DownloadProgress = $"Descargando... {p:0}%");

            await _updateService.DownloadAndInstallAsync(AvailableUpdate, progress);
            UpdateStatusText = "Instalador lanzado. La aplicación se cerrará en breve.";
        }
        catch (Exception ex)
        {
            UpdateStatusText = $"Error al descargar: {ex.Message}";
        }
        finally
        {
            IsDownloading = false;
            IsBusy = false;
        }
    }

    private bool CanDownloadUpdate() => IsUpdateAvailable && !IsDownloading;
}

