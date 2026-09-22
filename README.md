# Smart Calendar Manager 🚀

[![.NET 9](https://img.shields.io/badge/.NET-9.0-512BD4?logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/)
[![WinUI 3](https://img.shields.io/badge/WinUI-3.0-0078D4?logo=windows&logoColor=white)](https://learn.microsoft.com/windows/apps/winui/winui3/)
[![Windows App SDK](https://img.shields.io/badge/Windows_App_SDK-2.4-00A4EF?logo=windows11&logoColor=white)](https://github.com/microsoft/WindowsAppSDK)
[![Tests](https://img.shields.io/badge/Tests-xUnit%20(55%2F55%20Passed)-4EBA6F?logo=xunit&logoColor=white)](https://xunit.net/)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)

*Read this in [English](#english) | Léelo en [Español](#español)*

---

<a name="english"></a>
## English

### 1. Project Description
**Smart Calendar Manager** is a native Windows 11 desktop application designed to bridge personal and work schedules, automate video conference preparation, and launch scheduled desktop tools with a visual interface. It seamlessly synchronizes with **Google Calendar** via OAuth 2.0 and private RFC 5545 iCal feeds, automatically blocks personal events into work calendars with complete privacy ("🔒 Ocupada"), alerts and opens note-taking apps 5 minutes prior to eligible meetings, and triggers scheduled app/protocol launches without complex cron syntax.

### 2. Key Features
- 📅 **Daily Agenda & Video Meeting Detection:** Live view of today's meetings with instant join buttons (Google Meet, Zoom, Microsoft Teams, Webex). Auto-refreshes every 30 minutes in the background.
- 🔒 **Personal ➔ Work Availability Blocking (OAuth 2.0):**
  - Loopback IP OAuth 2.0 authorization with `Google.Apis.Auth` and `Google.Apis.Calendar.v3`.
  - Incremental sync using `SyncToken` for efficient quota usage and automated `410 Gone` full-resync recovery.
  - Generates private opaque block events in your work calendar to protect your personal details while reserving busy time slots.
  - Hardware-isolated Windows DPAPI encrypted credential storage (`ProtectedData`), avoiding plain-text tokens.
- 🥑 **Precision Pre-Meeting Automation:** Alerts you 5 minutes before qualifying meetings and launches companion tools like Granola. Includes a dismissible banner that remembers discarded meetings across app sessions.
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
- **Google Cloud APIs:** `Google.Apis.Calendar.v3` & `Google.Apis.Auth`
- **Security:** Windows Data Protection API (DPAPI) via `System.Security.Cryptography.ProtectedData`
- **Installer:** Inno Setup 6 / 7 script (`installer.iss`)
- **Unit Testing:** `xUnit` & `Moq` test suite (55 unit tests passing)

### 4. Key Learnings
- Architecting unpackaged WinUI 3 applications on .NET 9 with decoupled domain libraries (`SmartCalendarManager.Core`) for 100% testability.
- Safe OAuth 2.0 credential management in desktop apps using Windows DPAPI encryption instead of shared Credential Manager namespaces.
- Incremental state reconciliation against Google Calendar API using `SyncToken` and handling `410 Gone` gracefully.
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

# Restore dependencies and build solution
dotnet build SmartCalendarManager.sln -c Release

# Run tests
dotnet test tests/SmartCalendarManager.Tests/SmartCalendarManager.Tests.csproj

# Run application
dotnet run --project src/SmartCalendarManager.App/SmartCalendarManager.App.csproj
```

---

<a name="español"></a>
## Español

### 1. Descripción del Proyecto
**Smart Calendar Manager** es una aplicación de escritorio nativa para Windows 11 diseñada para conectar tus agendas personal y laboral, automatizar la preparación para videoconferencias y lanzar herramientas programadas mediante una interfaz visual intuitiva. Se sincroniza de forma transparente con **Google Calendar** vía OAuth 2.0 y feeds privados RFC 5545 iCal, bloquea automáticamente eventos personales en tu calendario laboral manteniendo total privacidad ("🔒 Ocupada"), notifica y abre herramientas de notas 5 minutos antes de reuniones y dispara lanzamientos programados sin necesidad de escribir sintaxis cron.

### 2. Funcionalidades Principales
- 📅 **Agenda Diaria y Detección de Videollamadas:** Vista en tiempo real de las reuniones del día con botón directo para unirse (Google Meet, Zoom, Microsoft Teams, Webex). Auto-refresco cada 30 minutos en segundo plano.
- 🔒 **Bloqueo de Disponibilidad Personal ➔ Laboral (OAuth 2.0):**
  - Flujo Loopback IP con `Google.Apis.Auth` y `Google.Apis.Calendar.v3`.
  - Sincronización incremental con `SyncToken` y recuperación automática ante errores `410 Gone`.
  - Generación de eventos de bloqueo privados ("🔒 Ocupada") en tu calendario laboral para proteger tu privacidad.
  - Cifrado seguro de credenciales con Windows DPAPI (`ProtectedData`) en lugar de archivos de texto plano.
- 🥑 **Automatización Pre-Reunión:** Alertas 5 minutos antes de reuniones elegibles con auto-apertura de Granola y banner descartable que recuerda eventos ignorados.
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
- **Google Cloud APIs:** `Google.Apis.Calendar.v3` y `Google.Apis.Auth`
- **Seguridad:** Windows Data Protection API (DPAPI)
- **Instalador:** Script Inno Setup (`installer.iss`)
- **Pruebas Unitarias:** xUnit y Moq (55 pruebas unitarias aprobadas)

### 4. Aprendizajes Clave
- Construcción de aplicaciones WinUI 3 unpackaged en .NET 9 con biblioteca de dominio desacoplada (`SmartCalendarManager.Core`) para 100% de testeabilidad.
- Gestión segura de credenciales OAuth en escritorio mediante cifrado Windows DPAPI en lugar de almacenes globales vulnerables.
- Reconciliación incremental bidireccional de eventos contra Google Calendar con `SyncToken` y manejo de `410 Gone`.
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

# Restaurar dependencias y compilar solución
dotnet build SmartCalendarManager.sln -c Release

# Ejecutar pruebas unitarias
dotnet test tests/SmartCalendarManager.Tests/SmartCalendarManager.Tests.csproj

# Iniciar aplicación
dotnet run --project src/SmartCalendarManager.App/SmartCalendarManager.App.csproj
```
