using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SmartCalendarManager.Core.Models;
using SmartCalendarManager.Core.Services;

namespace SmartCalendarManager.Core.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private readonly IGoogleCalendarService _calendarService;
    private readonly IUpdateService _updateService;

    [ObservableProperty]
    private string _iCalUrl = string.Empty;

    [ObservableProperty]
    private string _iCalKey = string.Empty;

    [ObservableProperty]
    private string _excludedKeywords = string.Empty;

    [ObservableProperty]
    private bool _ignoreAllDay = true;

    [ObservableProperty]
    private bool _requireLink;

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

    public SettingsViewModel(
        IGoogleCalendarService calendarService,
        IUpdateService updateService)
    {
        _calendarService = calendarService;
        _updateService = updateService;

        ICalUrl = _calendarService.ICalUrl ?? string.Empty;
        ICalKey = _calendarService.ICalKey ?? string.Empty;

        var f = _calendarService.FilterSettings;
        ExcludedKeywords = f.ExcludedKeywords;
        IgnoreAllDay = f.IgnoreAllDayEvents;
        RequireLink = f.RequireMeetingLink;

        CurrentVersion = _updateService.CurrentAppVersion;
    }

    [RelayCommand]
    public async Task SaveCalendarSettingsAsync()
    {
        IsBusy = true;
        StatusMessage = "Guardando y verificando feed iCal...";
        try
        {
            if (!string.IsNullOrWhiteSpace(ICalUrl))
            {
                bool valid = await _calendarService.SetICalCredentialsAsync(ICalUrl, ICalKey);
                if (!valid)
                {
                    StatusMessage = "Advertencia: La URL de iCal no parece ser un calendario válido.";
                }
            }
            else
            {
                await _calendarService.ClearICalCredentialsAsync();
            }

            var f = new CalendarFilterSettings
            {
                ExcludedKeywords = ExcludedKeywords,
                IgnoreAllDayEvents = IgnoreAllDay,
                RequireMeetingLink = RequireLink
            };
            _calendarService.UpdateFilterSettings(f);

            StatusMessage = "Configuración del calendario guardada con éxito.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error al guardar: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
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
}
