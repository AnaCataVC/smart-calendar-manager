# Reference Codebase Analysis: work-activity-panel

> Source project: `work-activity-panel` (WinUI 3 / .NET 9 desktop application)
> Target project: `smart-calendar-manager`
> Analysis date: 2026-09-21

---

## 1. Project Configuration (`WorkActivityPanel.csproj`)

### Build Targets & SDK
| Property | Value |
|---|---|
| SDK | `Microsoft.NET.Sdk` |
| OutputType | `WinExe` |
| TargetFramework | `net9.0-windows10.0.26100.0` |
| MinVersion | `10.0.17763.0` (Windows 10 1809) |
| UseWinUI | `true` |
| WindowsPackageType | `None` (unpackaged) |
| WindowsAppSDKSelfContained | `true` |
| Version | `2.6.0` |
| Nullable | `enable` |
| ImplicitUsings | `enable` |
| Platforms | `x86; x64; ARM64` |

### NuGet Packages
| Package | Version | Purpose |
|---|---|---|
| `Microsoft.WindowsAppSDK` | `2.4.0` | WinUI 3 runtime |
| `Microsoft.Windows.SDK.BuildTools` | `10.0.28000.2526` | Build tools |
| `Microsoft.Extensions.Hosting` | `9.0.2` | DI container, ILogger, IHostedService |
| `CommunityToolkit.Mvvm` | `8.4.0` | MVVM source generators |
| `H.NotifyIcon.WinUI` | `2.1.4` | System tray icon support |

> **No iCal parsing NuGet packages are used.** The iCal parser is fully custom and zero-dependency.

---

## 2. Models

### `CalendarEvent.cs`
**Namespace:** `WorkActivityPanel.Models`

| Property | Type | Notes |
|---|---|---|
| `Id` | `string` | UID from iCal feed |
| `Title` | `string` | SUMMARY field |
| `StartTime` | `DateTime` | Local time |
| `EndTime` | `DateTime` | Local time |
| `MeetingLink` | `string?` | Google Meet / Zoom / Teams / Webex URL |
| `OpensGranola` | `bool` | Default `true`; set by `CalendarFilterSettings.ShouldOpenGranola()` |
| `IsAllDay` | `bool` | Set by parser when `VALUE=DATE` or 23h+ duration |

**Computed Properties:**
- `FormattedStartTime` → `"h:mm tt"` or `"Todo el día"` if all-day
- `FormattedEndTime` → same pattern
- `IsPast` → `DateTime.Now > EndTime`
- `IsInProgress` → `Now >= StartTime && Now <= EndTime`
- `IsUpcoming` → `Now < StartTime`
- `StatusText` → `"En curso"` / `"Finalizada"` / `"Granola se abrirá 5 min antes"` / `"Solo en calendario"`

**Methods:**
- `Matches(CalendarEvent? other)` → structural equality (Id, StartTime, EndTime, Title, MeetingLink, OpensGranola, IsAllDay)

**Migration note:** `StatusText` strings are Granola-branded Spanish strings. Will be generalized/configurable. `OpensGranola` → rename to `TriggersAction`.

---

### `CalendarFilterSettings.cs`
**Namespace:** `WorkActivityPanel.Models`

| Property | Type | Default |
|---|---|---|
| `ExcludedKeywords` | `string` | `"[Personal], [Privado], Out of office, ..."` |
| `IgnoreAllDayEvents` | `bool` | `true` |
| `RequireMeetingLink` | `bool` | `false` |

**Methods:**
- `ShouldOpenGranola(CalendarEvent?)` → 3-rule filter (all-day, meeting link, keyword exclusion)

**Migration note:** `ShouldOpenGranola` → rename to `EvaluateEvent(CalendarEvent?)`.

---

## 3. Helpers

### `ICalParser.cs`
**Namespace:** `WorkActivityPanel.Helpers`
**File size:** 767 lines, 30 KB
**No external NuGet dependencies** — pure BCL.

**Public API:**
| Method | Signature | Purpose |
|---|---|---|
| `UnfoldLines` | `(string icsContent) → List<string>` | RFC 5545 line folding/unfolding |
| `ParseEventsForDate` | `(string icsContent, DateTime targetDate) → List<CalendarEvent>` | Main parsing entry point |
| `MatchesRecurrenceRule` | `(string rrule, DateTime dtStart, DateTime targetDate) → bool` | RRULE evaluator |
| `ParseDateTime` | `(string value) → DateTime?` | UTC/local iCal date-time parser |
| `UnescapeText` | `(string text) → string` | RFC 5545 escape characters |
| `ExtractMeetingLink` | `(string location, string description) → string?` | Regex meet link extractor |

**Regex (compile-time generated):**
Detects: Google Meet, Zoom, Microsoft Teams (microsoft.com and live.com), Webex.

**Parsing algorithm (2-pass):**
1. **Pass 1:** Collect all VEVENT blocks → extract EXDATE and RECURRENCE-ID.
2. **Pass 2:** Evaluate each event (overrides, recurring series, single instances).
3. Deduplication via `Dictionary<string, CalendarEvent>` keyed by UID.
4. Sort by StartTime ascending.

**RRULE support:** `DAILY`, `WEEKLY`, `MONTHLY`, `YEARLY`, with `INTERVAL`, `UNTIL`, `COUNT`, `BYDAY`, `BYMONTHDAY`, `WKST`.

**Migration note:** Migrate íntegro, only namespace rename needed.

---

### `LocalSettingsHelper.cs`
**Storage:** `%LocalAppData%\<AppName>\Data\settings.json`
**Format:** `Dictionary<string, string>` (flat key-value + JSON-serialized complex types)
**Thread safety:** `lock(LockObj)` on all read/write
**Test support:** `SettingsFilePath` is settable → allows temp file redirection in tests

**Settings keys used (Calendar):**
| Key | Type | Purpose |
|---|---|---|
| `"CalendarICalUrl"` | `string` | iCal feed URL |
| `"CalendarICalKey"` | `string` | Optional Basic Auth token |
| `"CalendarExcludedKeywords"` | `string` | Comma-separated excluded keywords |
| `"CalendarIgnoreAllDayEvents"` | `bool` (as string) | All-day filter toggle |
| `"CalendarRequireMeetingLink"` | `bool` (as string) | Meeting link filter toggle |

---

## 4. Services

### `GoogleCalendarService.cs`
**Implements:** `IGoogleCalendarService`, `IDisposable`

**Key operations:**
| Method | Description |
|---|---|
| `SetICalCredentialsAsync(url, key?)` | Validates URL by downloading feed, saves if valid |
| `ClearICalCredentialsAsync()` | Clears URL/key from memory and storage, stops timers |
| `GetTodayEventsAsync()` | Downloads feed, parses today's events, schedules alerts |
| `ScheduleMeetingAlerts(events)` | Creates `System.Threading.Timer` per qualifying event (-5 min) |
| `ClearAlerts()` | Disposes all active timers |
| `UpdateFilterSettings(settings)` | Updates and persists filter settings |

**Events:** `EventHandler<CalendarEvent>? UpcomingMeetingDetected`

**Timer pattern:**
- `ConcurrentBag<Timer>` for multiple simultaneous per-meeting alerts
- "Already in window" case: fires immediately if now is in the [-5 min, 0 min) window

**Migration note:** Tight coupling to `IAppLauncherService` (Granola) becomes pluggable `ICalendarAlertHandler` in new project.

---

### `AppLauncherService.cs`
**Implements:** `IAppLauncherService`

| Method | Description |
|---|---|
| `IsSlackRunning()` | Process check |
| `IsGranolaRunning()` | Process check |
| `LaunchGranola()` | Probes 5 standard install paths, fallback to URI scheme |
| `OpenUrl(string?)` | ShellExecute URL — handles null/blank silently |

**Migration note:** Will be extended to support configurable app/URI/protocol targets for the Cron Visual feature.

---

### `UpdateService.cs`
**Implements:** `IUpdateService`, `IDisposable`

| Method | Description |
|---|---|
| `CheckForUpdatesAsync(ct)` | GitHub Releases API, parses tag_name, finds .exe asset |
| `DownloadUpdateAsync(url, fileName, progress, ct)` | Streams to `%TEMP%\`, reports progress 0–100 |
| `LaunchInstaller(path)` | ShellExecute installer |

**Migration note:** Only `GitHubRepoOwner`/`GitHubRepoName` constants need updating. Full implementation reusable as-is.

---

## 5. Service Interfaces (all in `WorkActivityPanel.Services.Interfaces`)

### `IGoogleCalendarService`
| Member | Kind |
|---|---|
| `ICalUrl`, `ICalKey`, `IsConfigured`, `FilterSettings` | Properties |
| `UpcomingMeetingDetected` | Event |
| `SetICalCredentialsAsync`, `ClearICalCredentialsAsync`, `GetTodayEventsAsync` | Async methods |
| `ScheduleMeetingAlerts`, `ClearAlerts`, `UpdateFilterSettings` | Sync methods |

### `IAppLauncherService`
| Member | Kind |
|---|---|
| `IsSlackRunning`, `IsGranolaRunning` | `bool` methods |
| `EnsureSlackRunning`, `EnsureGranolaRunning`, `LaunchSlack`, `LaunchGranola`, `OpenUrl` | `void` methods |
| `GetGranolaExecutablePath` | `string?` method |

### `IUpdateService`
| Member | Kind |
|---|---|
| `CurrentAppVersion` | Property |
| `CheckForUpdatesAsync`, `DownloadUpdateAsync`, `DownloadAndInstallAsync` | Async methods |
| `LaunchInstaller` | Sync method |

---

## 6. Test Files

### Test Framework
- **Framework:** xUnit
- **Settings isolation:** `TestHelpers` redirects `LocalSettingsHelper.SettingsFilePath` to throwaway temp file per test scope

### `ICalParserTests.cs` — 16 tests
Coverage: UnfoldLines, ParseEventsForDate, all RRULE types (WEEKLY/DAILY/MONTHLY), EXDATE, RECURRENCE-ID, TZID handling, meeting link regex.

### `CalendarEventTests.cs` — 10 tests
Coverage: Constructor, IsPast/IsInProgress/IsUpcoming, StatusText strings, all-day formatting, pre-meeting window edge cases, Matches() equality.

### `CalendarFilterTests.cs` — 7 tests (40+ InlineData entries)
Coverage: All default excluded keywords, all-day toggle, RequireMeetingLink toggle, custom keyword lists.

### `AppLauncherServiceTests.cs` — 3 tests
Coverage: Smoke tests for process detection, path resolution.

---

## 7. Cross-Cutting Patterns

| Pattern | Usage |
|---|---|
| `async/await` | All I/O operations in GoogleCalendarService, UpdateService |
| `IDisposable` | GoogleCalendarService, UpdateService (timer cleanup) |
| `System.Threading.Timer` | Meeting alerts (ConcurrentBag), schedule timers |
| `EventHandler<T>` | UpcomingMeetingDetected, schedule transitions |
| `IProgress<double>` | Download progress in UpdateService |
| `ILogger<T>` | All services (Microsoft.Extensions.Logging) |
| `CancellationToken` | UpdateService async methods |
| `[GeneratedRegex]` | ICalParser.MeetingLinkRegex() |
| Interface segregation | Every service has a corresponding `I{ServiceName}` |

---

## 8. Migration Checklist

### Files to migrate (direct port + namespace rename):
- [ ] `Models/CalendarEvent.cs` — rename namespace, generalize StatusText strings
- [ ] `Models/CalendarFilterSettings.cs` — rename `ShouldOpenGranola` → `EvaluateEvent`
- [ ] `Helpers/ICalParser.cs` — namespace rename only, zero logic changes
- [ ] `Helpers/LocalSettingsHelper.cs` — rename namespace, update app path constant
- [ ] `Helpers/ProcessLaunchHelper.cs` — namespace rename only
- [ ] `Helpers/PathHelpers.cs` — namespace rename only
- [ ] `Services/GoogleCalendarService.cs` — decouple Granola alert via strategy/event pattern
- [ ] `Services/AppLauncherService.cs` — extend for configurable app/URI targets
- [ ] `Services/UpdateService.cs` — update GitHub repo constants only

### New services to create from scratch:
- [ ] `Services/GoogleOAuthSyncService.cs` — OAuth 2.0 bidirectional personal→work blocking
- [ ] `Services/DpapiFileDataStore.cs` — IDataStore implementation via DPAPI
- [ ] `Services/CronSchedulerService.cs` — Visual cron launcher (DayOfWeekFlags + Timer)
- [ ] `Services/CalendarSyncBackgroundService.cs` — BackgroundService for 30-min auto-refresh

### Test files to migrate:
- [ ] `ICalParserTests.cs` — all 16 tests portable, namespace rename only
- [ ] `CalendarEventTests.cs` — update Spanish strings if model changes
- [ ] `CalendarFilterTests.cs` — update `ShouldOpenGranola` rename
- [ ] `AppLauncherServiceTests.cs` — portable smoke tests

### Services NOT migrating:
- `DriveSyncService` / `IDriveSyncService` — Google Drive sync, not relevant
- `GitHubAuthService` / `IGitHubAuthService` — GitHub CLI switcher, not relevant
- `ScheduleService` / `IScheduleService` — work schedule concept replaced by visual cron
