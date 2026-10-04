<p align="center">
  <img src="icon.png" alt="smart-calendar-manager Logo" width="120" />
</p>

# Smart Calendar Manager

[English](README.md) | [Español](README.es.md)

[![.NET 9](https://img.shields.io/badge/.NET-9.0-512BD4?style=flat&logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/)
[![WinUI 3](https://img.shields.io/badge/WinUI-3.0-0078D4?style=flat&logo=windows&logoColor=white)](https://learn.microsoft.com/windows/apps/winui/winui3/)
[![Windows App SDK](https://img.shields.io/badge/Windows_App_SDK-2.4-00A4EF?style=flat&logo=windows11&logoColor=white)](https://github.com/microsoft/WindowsAppSDK)
[![Tests](https://img.shields.io/badge/Tests-xUnit%20(75%2F75%20Passed)-4EBA6F?style=flat&logo=xunit&logoColor=white)](https://xunit.net/)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg?style=flat)](LICENSE)

---

### 1. Project Description
**Smart Calendar Manager** is a native Windows 11 desktop application designed to bridge personal and work schedules, automate video conference preparation, and launch scheduled desktop tools with a visual interface. It reads **Google Calendar** (or any calendar) through secret RFC 5545 iCal feeds — one work feed plus any number of personal ones, no OAuth — generates an Apps Script that blocks personal events in the work calendar with complete privacy ("🔒 Ocupada"), alerts and opens note-taking apps a configurable number of minutes before meetings that have a video link, and triggers scheduled app/protocol launches without complex cron syntax.

### 2. Key Features
- 📅 **Daily Agenda & Video Meeting Detection:** Live view of today's meetings with instant join buttons (Google Meet, Zoom, Microsoft Teams, Webex). Auto-refreshes every 15 minutes in the background.
- 🗂️ **Multiple iCal Feeds, No OAuth:** Add work and personal secret iCal URLs; the agenda merges them with a Laboral/Personal tag, and one failing feed never hides the others.
- 🔒 **Personal ➔ Work Availability Blocking (Apps Script):** A setup wizard generates a Google Apps Script with your personal feeds embedded. It runs in the work account and creates private busy blocks, so it works even where the Workspace blocks third-party OAuth apps. See `docs/work-calendar-apps-script.md` (recurring events are not expanded).
- 🥑 **Precision Pre-Meeting Automation:** Alerts you before qualifying meetings (5 minutes by default, configurable) and launches companion tools like Granola, for work feeds only or for all feeds. By default only meetings with a video link qualify. Each eligible agenda item has an "Abrir Granola" button that also opens the meeting link. Includes a dismissible banner that remembers discarded meetings across app sessions.
- ⏰ **Visual Cron App Launcher:**
  - Friendly weekday pill selectors (Mon-Sun), `TimePicker`, and customizable destination targets.
  - Supports desktop executables, custom URI protocol schemes (`slack://`, `spotify://`), and browser URLs.
  - Precision minute evaluator backed by `System.Threading.Timer` with zero CPU overhead when idle.
- 💻 **Fluent Design & System Tray:** Native Windows 11 Mica backdrop, modern Fluent cards, minimize-to-system-tray with context flyout actions (`H.NotifyIcon.WinUI`).
- 🛡️ **Desktop Stability & Crash Resilience:** Global exception handlers dumping startup and runtime diagnostics to `%LOCALAPPDATA%\SmartCalendarManager\Logs`.
- 🚀 **In-App Updates & Packaging:** Integrated GitHub Releases update checker and Inno Setup installer script (`installer.iss`).

### 3. Technologies Used
- **UI Framework:** WinUI 3 (Windows App SDK 2.4 / 2.5) with Fluent Design & Mica backdrop
- **Runtime:** .NET 9 (`net9.0-windows10.0.26100.0`, unpackaged desktop app)
- **Architecture:** Clean Architecture + MVVM Pattern (`CommunityToolkit.Mvvm` 8.4)
- **Dependency Injection:** `Microsoft.Extensions.Hosting` & `Microsoft.Extensions.DependencyInjection`
- **System Tray:** `H.NotifyIcon.WinUI`
- **Calendar Engine:** Zero-dependency RFC 5545 iCalendar (`.ics`) parser with RRULE recurrence, TZID support, and meeting link regex extraction
- **Installer:** Inno Setup 6 / 7 script (`installer.iss`)
- **Unit Testing:** `xUnit` & `Moq` test suite

### 4. Key Learnings
- Architecting unpackaged WinUI 3 applications on .NET 9 with decoupled domain libraries (`SmartCalendarManager.Core`) for 100% testability.
- Working around Workspace OAuth restrictions by reading secret iCal feeds and moving calendar writes into a first-party Apps Script.
- RFC 5545 iCalendar folding, timezone resolution (IANA vs Windows TimeZoneInfo), and recurring series evaluation (RRULE) without heavy third-party libraries.
- Bitmask day-of-week manipulation (`DayOfWeekFlags`) for friendly visual cron scheduler interfaces.

### 5. Local Setup Instructions

#### Prerequisites
- Windows 10 (version 1809+) or Windows 11
- [.NET 9.0 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)

#### Building and Running
```powershell
# Clone the repository
git clone https://github.com/AnaCataVC/smart-calendar-manager.git
cd smart-calendar-manager

# Build the app (also builds Core)
dotnet build src/SmartCalendarManager.App/SmartCalendarManager.App.csproj -c Release -p:Platform=x64

# Run tests
dotnet test tests/SmartCalendarManager.Tests/SmartCalendarManager.Tests.csproj

# Run application
dotnet run --project src/SmartCalendarManager.App/SmartCalendarManager.App.csproj -p:Platform=x64
```

> **Known issue:** `dotnet build SmartCalendarManager.sln` currently fails with `NETSDK1032` (RuntimeIdentifier `win-x64` vs PlatformTarget `x86`) because the solution maps the App project to x86. Build the App project with `-p:Platform=x64` as above until the solution platforms are fixed.

#### Configuration
See [`docs/configuration.md`](docs/configuration.md) for calendar feeds, Granola scope, the Apps Script work-calendar blocking and migrating from the old OAuth version.

---


---

## License

This project is licensed under the MIT License. See [LICENSE](LICENSE) for details.

