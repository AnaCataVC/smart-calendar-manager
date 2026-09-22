> **Created:** 2026-09-21
> **Last Updated:** 2026-09-21

# Technology Stack Architecture Reference: WinUI 3, .NET 9 & Windows App SDK

This document provides a comprehensive, verified technical reference for developing high-performance, unpackaged desktop applications using WinUI 3, Windows App SDK 2.x, .NET 9, and the modern Microsoft desktop ecosystem.

---

## Table of Contents
1. [Windows App SDK 2.x & 2.4 Releases](#1-windows-app-sdk-2x--24-releases)
2. [WinUI 3 + .NET 9 Compatibility & Architecture](#2-winui-3--net-9-compatibility--architecture)
3. [CommunityToolkit.Mvvm 8.x](#3-communitytoolkitmvvm-8x)
4. [Microsoft.Extensions.Hosting in WinUI 3 Apps](#4-microsoftextensionshosting-in-winui-3-apps)
5. [System Tray Integration with H.NotifyIcon.WinUI](#5-system-tray-integration-with-hnotifyiconwinui)
6. [Google Calendar API & OAuth 2.0 Integration](#6-google-calendar-api--oauth-20-integration)
7. [Credential Storage & PasswordVault in Unpackaged Apps](#7-credential-storage--passwordvault-in-unpackaged-apps)
8. [Unit Testing: xUnit + Moq on .NET 9](#8-unit-testing-xunit--moq-on-net-9)
9. [Inno Setup Installer Patterns for Unpackaged WinUI 3](#9-inno-setup-installer-patterns-for-unpackaged-winui-3)
10. [H.NotifyIcon.WinUI Compatibility Matrix](#10-hnotifyiconwinui-compatibility-matrix)

---

## 1. Windows App SDK 2.x & 2.4 Releases

### Version Status & Release Information
* **Latest Stable Version:** `Microsoft.WindowsAppSDK` **2.5.1** (Released: September 16, 2026).
* **2.4 Line Status:** Version `2.4.1-experimental` (Released: August 25, 2026) preceded the 2.5 stable channel.
* **Architecture Evolution:** Windows App SDK 2.0+ marks the major transition from the 1.x series (1.5, 1.6, 1.7, 1.8), standardizing semantic versioning and modularizing runtime payloads.

### Key Features vs 1.x
1. **WinUI 3 Inking Support:** Introduction of `InkCanvas`, `InkToolbar`, and `InkPresenter` APIs natively in WinUI 3.
2. **On-Device AI Integration:** Direct runtime hooks for on-device language models, NPU hardware acceleration.
3. **Modernized Windowing & Content:** Enhanced `Microsoft.UI.Content` APIs for flexible anchor-based popup positioning.
4. **Enhanced Error Diagnostics:** Out-of-the-box Windows Error Reporting (WER) telemetry support for self-contained unpackaged .NET apps.
5. **Modernized Storage Pickers:** Overhauled file and folder picker surface providing modern Fluent dialogs even in unpackaged desktop processes.

### Unpackaged Deployment Support

#### Mode A: Self-Contained Deployment (Recommended for Inno Setup)
Bundles both the .NET 9 runtime and the Windows App SDK runtime binaries directly into the application distribution folder.
```xml
<PropertyGroup>
  <WindowsPackageType>None</WindowsPackageType>
  <WindowsAppSDKSelfContained>true</WindowsAppSDKSelfContained>
  <SelfContained>true</SelfContained>
  <EnableMsixTooling>true</EnableMsixTooling>
  <PublishTrimmed>false</PublishTrimmed>
</PropertyGroup>
```

#### Mode B: Framework-Dependent Deployment (Bootstrapper API)
Relies on an externally installed Windows App Runtime. The app initializes the runtime via the Bootstrapper API:
```csharp
Bootstrap.Initialize(0x00020005, "2.5");
```
* **Known 2.x Limitation:** Unpackaged framework-dependent applications resolve to the *newest compatible* installed 2.x runtime.

### Sources
* [Microsoft Learn: Windows App SDK Downloads](https://learn.microsoft.com/windows/apps/windows-app-sdk/downloads)
* [GitHub: Microsoft/WindowsAppSDK Repository](https://github.com/microsoft/WindowsAppSDK)
* [NuGet: Microsoft.WindowsAppSDK](https://www.nuget.org/packages/Microsoft.WindowsAppSDK)

---

## 2. WinUI 3 + .NET 9 Compatibility & Architecture

### Compatibility
WinUI 3 and Windows App SDK 2.x are fully compatible with .NET 9.

### Recommended `.csproj` Configuration
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>WinExe</OutputType>
    <TargetFramework>net9.0-windows10.0.22621.0</TargetFramework>
    <TargetPlatformMinVersion>10.0.17763.0</TargetPlatformMinVersion>
    <RootNamespace>SmartCalendarManager</RootNamespace>
    <ApplicationManifest>app.manifest</ApplicationManifest>
    <Platforms>x86;x64;ARM64</Platforms>
    <RuntimeIdentifiers>win-x86;win-x64;win-arm64</RuntimeIdentifiers>
    <UseWinUI>true</UseWinUI>
    <WindowsPackageType>None</WindowsPackageType>
    <WindowsAppSDKSelfContained>true</WindowsAppSDKSelfContained>
    <Nullable>enable</Nullable>
    <LangVersion>13.0</LangVersion>
    <PublishTrimmed>false</PublishTrimmed>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.WindowsAppSDK" Version="2.5.1" />
    <PackageReference Include="Microsoft.Windows.SDK.BuildTools" Version="10.0.26100.1742" />
    <PackageReference Include="CommunityToolkit.Mvvm" Version="8.4.2" />
    <PackageReference Include="Microsoft.Extensions.Hosting" Version="9.0.2" />
    <PackageReference Include="H.NotifyIcon.WinUI" Version="2.4.1" />
  </ItemGroup>
</Project>
```

### Decoupled Project Structure Best Practices
```
SmartCalendarManager/
├── src/
│   ├── SmartCalendarManager.Core/          # Pure .NET 9 class library (No WinUI/WinRT dependency)
│   └── SmartCalendarManager.App/           # WinUI 3 Presentation Layer
└── tests/
    └── SmartCalendarManager.Tests/         # Unit test project (xUnit + Moq)
```

### Critical WinUI 3 Stability Invariants
1. **Trimming Crash (`0xc000027b`):** Always ensure `<PublishTrimmed>false</PublishTrimmed>`.
2. **DispatcherQueue Ordering:** Initialize `App.DispatcherQueue` before creating `MainWindow`.
3. **TargetType Mismatch:** Never assign `Button` styles to compound controls in XAML.

### Sources
* [Microsoft Learn: WinUI 3 Overview](https://learn.microsoft.com/windows/apps/winui/winui3/)
* [Microsoft Learn: Develop unpackaged desktop apps with Windows App SDK](https://learn.microsoft.com/windows/apps/windows-app-sdk/deploy-unpackaged-apps)

---

## 3. CommunityToolkit.Mvvm 8.x

### Version & Enhancements
* **Latest Stable Version:** `CommunityToolkit.Mvvm` **8.4.2** (Built on Roslyn 5.0).
* **Key Innovation in 8.4:** Support for **partial properties** in addition to legacy field-backed properties.

### Modern Syntax (Version 8.4+ Partial Properties)
```csharp
public partial class CalendarEventViewModel : ObservableObject
{
    [ObservableProperty]
    public partial string Title { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TimeRemainingText))]
    [NotifyCanExecuteChangedFor(nameof(SyncCommand))]
    public partial DateTime StartTime { get; set; }

    [RelayCommand(CanExecute = nameof(CanSync))]
    private async Task SyncAsync(CancellationToken cancellationToken)
    {
        await Task.Delay(100, cancellationToken);
    }

    private bool CanSync => StartTime > DateTime.Now;
}
```

### Core Toolkit Attributes
* `[ObservableProperty]`: Generates INotifyPropertyChanged boilerplate with partial hook methods.
* `[RelayCommand]`: Generates IRelayCommand / IAsyncRelayCommand. Supports `IncludeCancelCommand = true`.
* `[NotifyPropertyChangedFor]`: Triggers notifications for calculated read-only properties.
* `[NotifyCanExecuteChangedFor]`: Informs bound commands to re-evaluate CanExecute.
* `WeakReferenceMessenger`: Loosely coupled pub/sub across background services and UI ViewModels.

### Sources
* [Microsoft Learn: MVVM Toolkit Documentation](https://learn.microsoft.com/dotnet/communitytoolkit/mvvm/)
* [NuGet: CommunityToolkit.Mvvm](https://www.nuget.org/packages/CommunityToolkit.Mvvm)

---

## 4. Microsoft.Extensions.Hosting in WinUI 3 Apps

### DI & Hosting Integration Pattern
```csharp
public partial class App : Application
{
    public static IHost AppHost { get; private set; } = null!;
    public static DispatcherQueue MainDispatcherQueue { get; private set; } = null!;

    public App()
    {
        InitializeComponent();

        AppHost = Host.CreateDefaultBuilder()
            .UseContentRoot(AppContext.BaseDirectory)
            .ConfigureServices((context, services) =>
            {
                services.AddSingleton<ICalendarSyncEngine, GoogleCalendarSyncEngine>();
                services.AddSingleton<ICredentialVault, DpapiCredentialVault>();
                services.AddTransient<MainViewModel>();
                services.AddHostedService<CalendarSyncBackgroundService>();
            })
            .Build();
    }

    protected override async void OnLaunched(LaunchActivatedEventArgs args)
    {
        base.OnLaunched(args);
        MainDispatcherQueue = DispatcherQueue.GetForCurrentThread();
        await AppHost.StartAsync();
        var mainWindow = new MainWindow();
        mainWindow.Activate();
    }
}
```

### Background Service (PeriodicTimer Pattern)
```csharp
public class CalendarSyncBackgroundService : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(30));
        while (!stoppingToken.IsCancellationRequested && await timer.WaitForNextTickAsync(stoppingToken))
        {
            await _syncEngine.SynchronizeEventsAsync(stoppingToken);
        }
    }
}
```

### Critical Threading Rule
Background workers MUST marshal UI updates via `App.MainDispatcherQueue.TryEnqueue(() => ...)` or `WeakReferenceMessenger`.

### Sources
* [Microsoft Learn: .NET Generic Host](https://learn.microsoft.com/dotnet/core/extensions/generic-host)

---

## 5. System Tray Integration with H.NotifyIcon.WinUI

### Overview
* **Latest Stable Version:** **2.4.1**
* **CRITICAL:** Install `H.NotifyIcon.WinUI`, NOT `H.NotifyIcon.Wpf`.

### XAML Setup
```xml
<tb:TaskbarIcon
    x:Name="MyTrayIcon"
    ToolTipText="Smart Calendar Manager"
    IconSource="ms-appx:///Assets/TrayIcon.ico"
    LeftClickCommand="{x:Bind ViewModel.ShowAppCommand}">
    <tb:TaskbarIcon.ContextFlyout>
        <MenuFlyout>
            <MenuFlyoutItem Text="Open Smart Calendar" Command="{x:Bind ViewModel.ShowAppCommand}" />
            <MenuFlyoutItem Text="Sync Now" Command="{x:Bind ViewModel.SyncNowCommand}" />
            <MenuFlyoutSeparator />
            <MenuFlyoutItem Text="Exit" Command="{x:Bind ViewModel.ExitAppCommand}" />
        </MenuFlyout>
    </tb:TaskbarIcon.ContextFlyout>
</tb:TaskbarIcon>
```

### Known Issues & Solutions
| Issue | Solution |
|---|---|
| Tray icon disappears | Keep permanent static reference in `App.xaml.cs` |
| Flyout clips behind taskbar | Set `ContextMenuMode="SecondWindow"` |
| Build error: missing `TaskbarIcon` | Verify package is `H.NotifyIcon.WinUI`, not WPF version |

### Sources
* [GitHub: HavenDV/H.NotifyIcon](https://github.com/HavenDV/H.NotifyIcon)
* [NuGet: H.NotifyIcon.WinUI](https://www.nuget.org/packages/H.NotifyIcon.WinUI)

---

## 6. Google Calendar API & OAuth 2.0 Integration

### Package Versions
* `Google.Apis.Auth`: **1.76.0**
* `Google.Apis.Calendar.v3`: **1.75.0.4206**

### OAuth 2.0 Loopback Flow
```csharp
UserCredential credential = await GoogleWebAuthorizationBroker.AuthorizeAsync(
    secrets,
    _scopes,
    "user",
    CancellationToken.None,
    tokenStore,
    new LocalServerCodeReceiver()
);
```

### Incremental Sync with SyncToken
1. **Initial Sync:** Query `Events.List()` → paginate → save `NextSyncToken`.
2. **Incremental:** Set `SyncToken = stored` → **DO NOT** supply `TimeMin`/`TimeMax`/`OrderBy`.
3. **410 Gone handling:** Clear token → perform full resync automatically.

```csharp
catch (GoogleApiException ex) when (ex.HttpStatusCode == HttpStatusCode.Gone)
{
    await _tokenStore.ClearSyncTokenAsync();
    return await PerformFullSyncAsync(service, calendarId);
}
```

### Scopes
* Read-only: `CalendarService.Scope.CalendarReadonly`
* Write events: `CalendarService.Scope.CalendarEvents`

### Sources
* [Google Calendar API: Synchronize Resources Efficiently](https://developers.google.com/calendar/api/guides/sync)
* [NuGet: Google.Apis.Calendar.v3](https://www.nuget.org/packages/Google.Apis.Calendar.v3)

---

## 7. Credential Storage & PasswordVault in Unpackaged Apps

### PasswordVault Risk Analysis for Unpackaged Apps
1. **No AppContainer isolation:** Credentials stored in global user Credential Manager — any user-session process can read them.
2. **Packaging migration breaks access:** Switching between unpackaged/packaged orphans existing credentials.
3. **Resource collisions:** Must use explicit, globally unique resource keys.

### Recommended Solution: Windows DPAPI (`ProtectedData`)
Implements `IDataStore` interface for Google API client using `ProtectedData.Protect(DataProtectionScope.CurrentUser)`.

```csharp
public class DpapiFileDataStore : IDataStore
{
    public Task StoreAsync<T>(string key, T value)
    {
        byte[] encrypted = ProtectedData.Protect(
            Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value)),
            null, DataProtectionScope.CurrentUser);
        File.WriteAllBytes(GetFilePath(key), encrypted);
        return Task.CompletedTask;
    }
}
```

> **Decision:** Use DPAPI (`ProtectedData`) rather than `PasswordVault` for unpackaged app security.

### Sources
* [Microsoft Learn: ProtectedData Class (DPAPI)](https://learn.microsoft.com/dotnet/api/system.security.cryptography.protecteddata)

---

## 8. Unit Testing: xUnit + Moq on .NET 9

### Versions
* **xUnit.net v3:** Package `xunit.v3` **4.0.1** (full parallelization, cancellation token support)
* **Moq:** **4.20.72** (SponsorLink telemetry permanently removed)
* **FluentAssertions:** **8.0.1**

### Test Project Configuration
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net9.0</TargetFramework>
    <!-- Target net9.0, NOT net9.0-windows — avoids WinRT dependencies in tests -->
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.13.0" />
    <PackageReference Include="xunit.v3" Version="4.0.1" />
    <PackageReference Include="xunit.runner.visualstudio" Version="3.0.2" />
    <PackageReference Include="Moq" Version="4.20.72" />
    <PackageReference Include="FluentAssertions" Version="8.0.1" />
  </ItemGroup>
</Project>
```

### Sources
* [xUnit.net Documentation](https://xunit.net/)
* [NuGet: xunit.v3](https://www.nuget.org/packages/xunit.v3)

---

## 9. Inno Setup Installer Patterns for Unpackaged WinUI 3

### Recommended Versions
* **Inno Setup 6.4 / 6.7.3:** Stable, standard release.
* **Inno Setup 7.0.2:** Native 64-bit installer architecture.

### Publish Command (Self-Contained)
```powershell
dotnet publish src/SmartCalendarManager.App/SmartCalendarManager.App.csproj `
  -c Release -r win-x64 --self-contained true `
  /p:WindowsPackageType=None /p:WindowsAppSDKSelfContained=true `
  /p:PublishTrimmed=false -o .\dist\publish\win-x64
```

### Key Inno Setup Script Settings
```pascal
[Setup]
ArchitecturesAllowed=x64compatible
PrivilegesRequired=lowest
WindowsPackageType=None  ; unpackaged mode

[Code]
function InitializeSetup(): Boolean;
var ErrorCode: Integer;
begin
  ShellExec('open', 'taskkill.exe', '/F /IM SmartCalendarManager.exe', '', SW_HIDE, ewWaitUntilTerminated, ErrorCode);
  Result := True;
end;
```

### Sources
* [JRSoftware: Inno Setup](https://jrsoftware.org/isinfo.php)

---

## 10. H.NotifyIcon.WinUI Compatibility Matrix

### Verified Compatibility
* **H.NotifyIcon.WinUI 2.4.1** is compatible with Windows App SDK 2.5.1 and .NET 9.
* Target Framework: Works with `net9.0-windows10.0.22621.0`.

### Compatibility Table
| H.NotifyIcon.WinUI | Windows App SDK | .NET | Status |
|---|---|---|---|
| 2.4.1 | 2.5.1 | 9.0 | ✅ Verified |
| 2.4.1 | 1.6.x | 8.0 | ✅ Verified |
| 2.3.x | 1.5.x | 8.0 | ✅ Works |

### Sources
* [GitHub: HavenDV/H.NotifyIcon Issues & Wiki](https://github.com/HavenDV/H.NotifyIcon/issues)
* [NuGet: H.NotifyIcon.WinUI 2.4.1](https://www.nuget.org/packages/H.NotifyIcon.WinUI)
