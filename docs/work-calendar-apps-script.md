# Blocking the work calendar with Apps Script

The app never writes to any calendar: it only reads secret iCal URLs. To mirror personal events as private busy blocks in the work calendar, it generates a Google Apps Script that runs inside the **work** account. Apps Script is a first-party Google product, so it works even when the Workspace admin blocks third-party OAuth apps. It still fails if the admin has also disabled Apps Script or `UrlFetchApp`.

## Setup

In the app: *Configuración → Bloqueo en calendario laboral (Apps Script)*. The script embeds the URLs of every enabled **Personal** feed and the block title you choose.

1. Signed in with the work account, open <https://script.google.com> → *New project* (the "Abrir script.google.com" button does this).
2. Press "Copiar script" and paste it over the contents of `Code.gs`.
3. Run `syncBlocks` once and accept the permissions (Calendar and external requests).
4. *Triggers* → add a trigger: function `syncBlocks`, time-driven, every 15 minutes.
5. After adding or removing personal feeds in the app, copy the script again and replace it.

## How it works

- Fetches each personal iCal URL with `UrlFetchApp` and parses `VEVENT` blocks: `DTSTART`/`DTEND` in all-day (`DATE`), UTC (`...Z`), `TZID=...` and floating forms; events with `STATUS:CANCELLED` are skipped.
- Creates a private event titled with the block title in the default work calendar for each personal event in the next 30 days, tagged so the script only ever touches its own blocks.
- Blocks are matched by time range, so moving a personal event deletes the old block and creates a new one on the next run.
- If any feed fails to download, or returns something that is not an iCal calendar (for example a login page with status 200), the run aborts before touching the calendar, so existing blocks are never deleted by mistake.

## Known limits

- **Recurring events are not expanded.** `RRULE` is ignored: only the first occurrence of a series is considered, so a weekly personal event that started in the past produces no blocks.
- **The secret personal URLs live inside a script in the work account.** Anyone with access to that script project can read the personal calendars. Reset the secret address in Google Calendar if the script is ever shared or the work account is handed over.
