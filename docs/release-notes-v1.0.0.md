# Release v1.0.0

## Features
- **Daily Agenda View:** Real-time visualization of daily scheduled meetings with one-click direct access to video conference calls (Google Meet, Zoom, Microsoft Teams, Webex) and automated 30-minute background refresh.
- **Personal to Work Calendar Availability Blocking (OAuth 2.0):** Incremental synchronization using `SyncToken` with automatic `410 Gone` full-resync recovery. Dynamically injects private opaque blocking events ("🔒 Ocupada") into the work calendar to guard personal schedule details.
- **Hardware-Isolated Credential Security (Windows DPAPI):** Token storage secured with Windows Data Protection API (`ProtectedData.Protect`), preventing plain-text token exposure in unpackaged environments.
- **Pre-Meeting Automation:** Companion launch trigger that initiates note-taking tools 5 minutes prior to eligible meetings, backed by a persistent dismissible banner.
- **Visual Cron Application Launcher:** Declarative weekday pill selector (Mon-Sun) and time picker to schedule executable applications, custom URI schemes (`slack://`), and URLs without cron syntax.
- **Native Fluent Design & System Tray Integration:** Windows 11 Mica backdrop with seamless minimize-to-tray capability and context menu flyout controls via `H.NotifyIcon.WinUI`.
- **Global Error Logging:** Automatic crash dump and startup telemetry tracking saved to `%LOCALAPPDATA%\SmartCalendarManager\Logs`.
- **Integrated Auto-Updater:** Built-in update detection via GitHub Releases API with streaming download progress.

---

# Lanzamiento v1.0.0

## Nuevas Funcionalidades
- **Vista de Agenda Diaria:** Visualización en tiempo real de las reuniones del día con acceso directo en un clic a videollamadas (Google Meet, Zoom, Microsoft Teams, Webex) y auto-refresco en segundo plano cada 30 minutos.
- **Bloqueo de Disponibilidad Personal a Laboral (OAuth 2.0):** Sincronización incremental con `SyncToken` y recuperación automática ante errores `410 Gone`. Inserta eventos de bloqueo privados y opacos ("🔒 Ocupada") en el calendario de trabajo para resguardar la privacidad personal.
- **Seguridad de Credenciales Aislada por Hardware (Windows DPAPI):** Almacenamiento seguro de tokens OAuth mediante Windows Data Protection API (`ProtectedData.Protect`), evitando tokens en texto plano en entornos unpackaged.
- **Automatización Pre-Reunión:** Disparador que inicia herramientas complementarias de notas 5 minutos antes de reuniones elegibles, con banner descartable que persiste reuniones ignoradas.
- **Lanzador de Aplicaciones con Cron Visual:** Selector visual de días de la semana (Lun-Dom) y selector de hora para programar ejecutables, protocolos URI personalizados (`slack://`) y enlaces web sin sintaxis cron.
- **Fluent Design Nativo e Integración en Bandeja del Sistema:** Fondo Mica de Windows 11 con soporte para minimizar en la bandeja y controles de menú contextual con `H.NotifyIcon.WinUI`.
- **Registro Global de Diagnóstico:** Manejador global de excepciones y volcado de telemetría de arranque en `%LOCALAPPDATA%\SmartCalendarManager\Logs`.
- **Actualizador Integrado:** Verificación de nuevas versiones vía GitHub Releases API con descarga en streaming y reporte de progreso.
