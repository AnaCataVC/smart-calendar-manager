using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SmartCalendarManager.Core.Models;
using SmartCalendarManager.Core.Services;

namespace SmartCalendarManager.Core.ViewModels;

public partial class SyncSettingsViewModel : ObservableObject
{
    private readonly IGoogleOAuthSyncService _syncService;

    [ObservableProperty]
    private string _clientId = string.Empty;

    [ObservableProperty]
    private string _clientSecret = string.Empty;

    [ObservableProperty]
    private string _workCalendarId = string.Empty;

    [ObservableProperty]
    private string _blockTitle = "🔒 Ocupada";

    [ObservableProperty]
    private bool _autoSyncEnabled = true;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private string _lastSyncText = "Nunca";

    public bool IsAuthorized => _syncService.IsAuthorized;

    public SyncSettingsViewModel(IGoogleOAuthSyncService syncService)
    {
        _syncService = syncService;

        var s = _syncService.Settings;
        ClientId = s.ClientId;
        ClientSecret = s.ClientSecret;
        WorkCalendarId = s.WorkCalendarId;
        BlockTitle = s.BlockEventTitle;
        AutoSyncEnabled = s.AutoSyncEnabled;
        LastSyncText = s.LastSyncTime?.ToLocalTime().ToString("g") ?? "Nunca";
    }

    [RelayCommand]
    public async Task ConnectGoogleAsync()
    {
        IsBusy = true;
        StatusMessage = "Conectando con Google...";
        try
        {
            bool ok = await _syncService.AuthorizeAsync(ClientId, ClientSecret);
            if (ok)
            {
                SaveSettings();
                StatusMessage = "¡Cuenta de Google conectada exitosamente!";
                OnPropertyChanged(nameof(IsAuthorized));
            }
            else
            {
                StatusMessage = "Error de autorización. Verifica tus credenciales.";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    public async Task DisconnectGoogleAsync()
    {
        IsBusy = true;
        try
        {
            await _syncService.SignOutAsync();
            StatusMessage = "Desconectado de Google.";
            OnPropertyChanged(nameof(IsAuthorized));
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error al desconectar: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    public async Task SyncNowAsync()
    {
        IsBusy = true;
        StatusMessage = "Sincronizando bloqueos con calendario laboral...";
        try
        {
            SaveSettings();
            int count = await _syncService.SynchronizeAvailabilityAsync();
            LastSyncText = DateTime.Now.ToString("g");
            StatusMessage = $"Sincronización completa ({count} eventos procesados).";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error de sincronización: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    public void SaveSettings()
    {
        var s = _syncService.Settings;
        s.ClientId = ClientId;
        s.ClientSecret = ClientSecret;
        s.WorkCalendarId = WorkCalendarId;
        s.BlockEventTitle = BlockTitle;
        s.AutoSyncEnabled = AutoSyncEnabled;
        _syncService.UpdateSettings(s);
        StatusMessage = "Configuración de sincronización guardada.";
    }
}
