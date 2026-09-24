# Configuration

Everything lives under **Configuración** (the gear at the bottom of the navigation pane). Settings are stored in `%LOCALAPPDATA%\SmartCalendarManager\Data\settings.json`.

## Calendar feeds

The app reads calendars only through iCal (`.ics`) URLs. It never signs in to Google and never writes to a calendar.

- Add one **Laboral** feed (your work calendar) and any number of **Personal** feeds.
- Each feed can be turned on or off, or removed. Toggling a feed on or off takes effect when you press *Guardar Configuración*. Adding or removing a feed takes effect immediately.
- The agenda merges every enabled feed, sorted by time, and tags each event with its kind and feed name. If a feed fails to download, the others still show up and the status line names the one that failed.
- The *Contraseña HTTP Basic* field is only for feeds served behind HTTP Basic authentication. Google's secret addresses do not need it.

### Getting Google Calendar's secret iCal address

1. Open Google Calendar on the web → *Settings* → select the calendar under *Settings for my calendars*.
2. Under *Integrate calendar*, copy **Secret address in iCal format** (it contains `/private-`).
3. Paste it as a new feed in the app.

Anyone with that URL can read the calendar. If it leaks, use *Reset* next to the secret address in Google Calendar and update the feed in the app. Some Workspace admins disable secret addresses. If that is the case for your work calendar, you have no way to get one.

## Granola

- Granola opens before each eligible meeting (5 minutes by default, adjustable from 0 to 60 in *Minutos de anticipación*), and an *Abrir Granola* button appears on eligible agenda items. The button launches Granola and opens the meeting link if there is one.
- **Scope:** *Solo calendarios laborales* (default) or *Calendarios laborales y personales*. Saving the configuration refreshes the agenda and reschedules the pre-meeting timers, so a narrower scope applies right away.
- By default only events with a video meeting link (Google Meet, Zoom, Teams, Webex) in their location or description are eligible. Turn off *Abrir Granola solo si hay enlace de reunión* under *Filtros de eventos* to include events without a link.
- The other event filters (excluded keywords, ignore all-day events) also decide which events open Granola automatically.

## Blocking personal time on the work calendar

Under *Bloqueo en calendario laboral (Apps Script)* the app generates a Google Apps Script that you run in your work account. It copies your enabled personal feeds as private busy blocks. Setup steps, behavior and limits are in [`work-calendar-apps-script.md`](work-calendar-apps-script.md). In short:

- Recurring personal events (`RRULE`) are not expanded.
- The secret personal URLs end up inside a script in the work account.
- Copy the script again whenever your personal feeds change.

## Migrating from the OAuth version (v1.0.0)

- The single iCal URL (and its optional HTTP Basic password) from v1.0.0 is converted automatically into one **Laboral** feed on first launch.
- The OAuth *Sincronización Personal ➔ Laboral* tab, the Google Client ID/Secret and the stored OAuth tokens are no longer used. Blocks created by the old OAuth sync stay in the work calendar. Delete them manually if you do not want them. The Apps Script only manages its own blocks.
- The OAuth tokens that v1.0.0 stored in `%LOCALAPPDATA%\SmartCalendarManager\OAuthTokens` can be deleted.
