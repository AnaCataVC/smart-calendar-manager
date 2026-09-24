# Smart Calendar Manager 🚀

[![.NET 9](https://img.shields.io/badge/.NET-9.0-512BD4?logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/)
[![WinUI 3](https://img.shields.io/badge/WinUI-3.0-0078D4?logo=windows&logoColor=white)](https://learn.microsoft.com/windows/apps/winui/winui3/)
[![Windows App SDK](https://img.shields.io/badge/Windows_App_SDK-2.4-00A4EF?logo=windows11&logoColor=white)](https://github.com/microsoft/WindowsAppSDK)
[![Tests](https://img.shields.io/badge/Tests-xUnit%20(75%2F75%20Passed)-4EBA6F?logo=xunit&logoColor=white)](https://xunit.net/)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)

*Read this in [English](#english) | Léelo en [Español](#español)*

---

<a name="english"></a>
## English

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

<a name="español"></a>
## Español

### 1. Descripción del Proyecto
**Smart Calendar Manager** es una aplicación de escritorio nativa para Windows 11 diseñada para conectar tus agendas personal y laboral, automatizar la preparación para videoconferencias y lanzar herramientas programadas mediante una interfaz visual intuitiva. Lee **Google Calendar** (o cualquier calendario) mediante feeds iCal RFC 5545 secretos — un feed laboral y los personales que quieras, sin OAuth —, genera un Apps Script que bloquea tus eventos personales en el calendario laboral manteniendo total privacidad ("🔒 Ocupada"), notifica y abre herramientas de notas unos minutos antes (configurable) de reuniones con enlace de videollamada y dispara lanzamientos programados sin necesidad de escribir sintaxis cron.

### 2. Funcionalidades Principales
- 📅 **Agenda Diaria y Detección de Videollamadas:** Vista en tiempo real de las reuniones del día con botón directo para unirse (Google Meet, Zoom, Microsoft Teams, Webex). Auto-refresco cada 15 minutos en segundo plano.
- 🗂️ **Varios Feeds iCal, Sin OAuth:** Agrega URLs iCal secretas laborales y personales; la agenda las combina con una etiqueta Laboral/Personal, y si un feed falla los demás se siguen mostrando.
- 🔒 **Bloqueo de Disponibilidad Personal ➔ Laboral (Apps Script):** Un asistente genera un Google Apps Script con tus feeds personales incluidos. Corre en la cuenta laboral y crea bloqueos privados, así que funciona aunque el Workspace bloquee apps OAuth de terceros. Ver `docs/work-calendar-apps-script.md` (los eventos recurrentes no se expanden).
- 🥑 **Automatización Pre-Reunión:** Alertas antes de reuniones elegibles (5 minutos por defecto, configurable) con auto-apertura de Granola solo en reuniones con enlace de videollamada (solo feeds laborales o todos), botón "Abrir Granola" en la agenda que además abre el enlace de la reunión, y banner descartable que recuerda eventos ignorados.
- ⏰ **Lanzador de Apps Programado (Cron Visual):**
  - Píldoras amigables de días (L M X J V S D), `TimePicker` y selección de app/URI destino (`slack://`, ejecutables o URLs web).
  - Motor de ejecución con `System.Threading.Timer` y evaluación de máscaras de bits con 0% de consumo de CPU en reposo.
- 💻 **Fluent Design y Bandeja del Sistema:** Fondo Mica nativo de Windows 11, tarjetas Fluent y minimización a la bandeja del sistema con `H.NotifyIcon.WinUI`.
- 🛡️ **Estabilidad Desktop y Diagnóstico:** Manejadores globales de excepciones con volcado a disco en `%LOCALAPPDATA%\SmartCalendarManager\Logs`.
- 🚀 **Actualizaciones e Instalador:** Verificador de versiones vía GitHub Releases API y script de instalador Inno Setup (`installer.iss`).

### 3. Tecnologías Utilizadas
- **Framework de UI:** WinUI 3 (Windows App SDK 2.4 / 2.5) con Fluent Design y fondo Mica
- **Entorno de ejecución:** .NET 9 (`net9.0-windows10.0.26100.0`, unpackaged)
- **Arquitectura:** Arquitectura Limpia + MVVM (`CommunityToolkit.Mvvm` 8.4)
- **Inyección de Dependencias:** `Microsoft.Extensions.Hosting`
- **Bandeja del Sistema:** `H.NotifyIcon.WinUI`
- **Motor de Calendario:** Parser RFC 5545 iCalendar (`.ics`) sin dependencias externas con soporte de RRULE, TZID y regex para links de videollamada
- **Instalador:** Script Inno Setup (`installer.iss`)
- **Pruebas Unitarias:** suite de pruebas con xUnit y Moq

### 4. Aprendizajes Clave
- Construcción de aplicaciones WinUI 3 unpackaged en .NET 9 con biblioteca de dominio desacoplada (`SmartCalendarManager.Core`) para 100% de testeabilidad.
- Sortear las restricciones OAuth de Workspace leyendo feeds iCal secretos y moviendo la escritura del calendario a un Apps Script de primera parte.
- Evaluación de recurrencias complejas RFC 5545 (RRULE) y conversión de zonas horarias IANA/Windows con zero dependencias externas.
- Manipulación de máscaras de bits (`DayOfWeekFlags`) para interfaces de usuario amigables tipo cron visual.

### 5. Instrucciones de Instalación Local

#### Requisitos Previos
- Windows 10 (versión 1809+) o Windows 11
- [.NET 9.0 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)

#### Compilación y Ejecución
```powershell
# Clonar el repositorio
git clone https://github.com/AnaCataVC/smart-calendar-manager.git
cd smart-calendar-manager

# Compilar la app (también compila Core)
dotnet build src/SmartCalendarManager.App/SmartCalendarManager.App.csproj -c Release -p:Platform=x64

# Ejecutar pruebas unitarias
dotnet test tests/SmartCalendarManager.Tests/SmartCalendarManager.Tests.csproj

# Iniciar aplicación
dotnet run --project src/SmartCalendarManager.App/SmartCalendarManager.App.csproj -p:Platform=x64
```

> **Problema conocido:** `dotnet build SmartCalendarManager.sln` hoy falla con `NETSDK1032` (RuntimeIdentifier `win-x64` vs PlatformTarget `x86`) porque la solución asigna el proyecto App a x86. Compila el proyecto App con `-p:Platform=x64` como arriba mientras no se corrijan las plataformas de la solución.

#### Configuración
Ver [`docs/configuration.md`](docs/configuration.md) para los feeds de calendario, el alcance de Granola, el bloqueo del calendario laboral con Apps Script y la migración desde la versión con OAuth.
