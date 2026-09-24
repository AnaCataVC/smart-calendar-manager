using System.Collections.Generic;
using System.Linq;
using System.Text.Encodings.Web;
using System.Text.Json;
using SmartCalendarManager.Core.Models;

namespace SmartCalendarManager.Core.Services;

/// <summary>
/// Builds the Google Apps Script that copies personal iCal events as private blocks into the work calendar.
/// The script runs in the work account (script.google.com); the app only generates it.
/// </summary>
public static class AppsScriptGenerator
{
    // Relaxed escaping keeps URLs readable ('&' stays '&') while still producing valid JS string literals.
    private static readonly JsonSerializerOptions JsLiteralOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        WriteIndented = true,
        NewLine = "\n"
    };

    public static string Build(IEnumerable<CalendarFeed> personalFeeds, string blockTitle)
    {
        var urls = personalFeeds.Select(f => f.Url.Trim()).Where(u => u.Length > 0).ToList();
        var urlsLiteral = JsonSerializer.Serialize(urls, JsLiteralOptions);
        var titleLiteral = JsonSerializer.Serialize(blockTitle, JsLiteralOptions);

        return $"const ICAL_URLS = {urlsLiteral};\n"
             + $"const BLOCK_TITLE = {titleLiteral};\n"
             + ScriptBody;
    }

    private const string ScriptBody = """
        const DAYS_AHEAD = 30;
        const TAG = 'personalBlock';

        // Recurring events (RRULE) are not expanded: only their first occurrence becomes a block.
        function syncBlocks() {
          const now = new Date();
          const until = new Date(now.getTime() + DAYS_AHEAD * 86400000);

          // A failing fetch throws and aborts the run, so existing blocks are never deleted on a network error.
          const busy = [];
          ICAL_URLS.forEach(url => {
            const ics = UrlFetchApp.fetch(url).getContentText();
            // A 200 that is not a calendar (e.g. a login page) would otherwise read as "no events" and wipe the blocks.
            if (ics.indexOf('BEGIN:VCALENDAR') < 0) throw new Error('Not an iCal feed: ' + url);
            parseEvents(ics)
              .filter(b => b.end > now && b.start < until)
              .forEach(b => busy.push(b));
          });

          const work = CalendarApp.getDefaultCalendar();
          const existing = work.getEvents(now, until).filter(e => e.getTag(TAG) === '1');
          const key = (s, e) => s.getTime() + '|' + e.getTime();

          const wanted = new Set(busy.map(b => key(b.start, b.end)));
          const have = new Set();

          existing.forEach(ev => {
            const k = key(ev.getStartTime(), ev.getEndTime());
            if (wanted.has(k) && !have.has(k)) have.add(k);
            else ev.deleteEvent();
          });

          busy.forEach(b => {
            const k = key(b.start, b.end);
            if (have.has(k)) return;
            have.add(k);
            const ev = work.createEvent(BLOCK_TITLE, b.start, b.end);
            ev.setVisibility(CalendarApp.Visibility.PRIVATE);
            ev.setTag(TAG, '1');
          });
        }

        function parseEvents(ics) {
          const lines = ics.replace(/\r?\n[ \t]/g, '').split(/\r?\n/);
          const events = [];
          let cur = null;
          lines.forEach(line => {
            if (line === 'BEGIN:VEVENT') { cur = {}; return; }
            if (line === 'END:VEVENT') {
              if (cur && cur.start && !cur.cancelled) {
                if (!cur.end) cur.end = new Date(cur.start.getTime() + (cur.allDay ? 86400000 : 0));
                events.push(cur);
              }
              cur = null;
              return;
            }
            if (!cur) return;
            const i = line.indexOf(':');
            if (i < 0) return;
            const head = line.slice(0, i);
            const value = line.slice(i + 1).trim();
            const name = head.split(';')[0].toUpperCase();
            if (name === 'DTSTART') { cur.start = parseDate(head, value); cur.allDay = /^\d{8}$/.test(value); }
            else if (name === 'DTEND') cur.end = parseDate(head, value);
            else if (name === 'STATUS' && value.toUpperCase() === 'CANCELLED') cur.cancelled = true;
          });
          return events;
        }

        // Handles DATE (all-day), UTC (...Z), TZID=... and floating DATE-TIME values.
        function parseDate(head, value) {
          const m = value.match(/^(\d{4})(\d{2})(\d{2})(?:T(\d{2})(\d{2})(\d{2})(Z)?)?$/);
          if (!m) return null;
          if (!m[4]) return new Date(+m[1], +m[2] - 1, +m[3]);
          if (m[7]) return new Date(Date.UTC(+m[1], +m[2] - 1, +m[3], +m[4], +m[5], +m[6]));
          const tzMatch = head.match(/TZID=("?)([^";:]+)\1/);
          const tz = tzMatch ? tzMatch[2] : Session.getScriptTimeZone();
          return Utilities.parseDate(m[1] + '-' + m[2] + '-' + m[3] + ' ' + m[4] + ':' + m[5] + ':' + m[6], tz, 'yyyy-MM-dd HH:mm:ss');
        }
        """;
}
