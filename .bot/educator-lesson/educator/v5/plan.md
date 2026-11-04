# v5 — Help for every admin page (Hópkaup + Heilsumynd)

Request (Ingi via plang-me, 2026-11-03): the same kind of help as the Hópkaup CRM course for all admin pages in Hópkaup and Heilsumynd. Admin UI and help are in English; customer and nurse screens are in Icelandic.

## Heilsumynd
- Same format as hjalp/greinar and hjalp/tests: index.html + images/ + audio/ (mp4 + webm), Puck voice, speed button, no autoplay.
- Hand over hjalp/NAME/ with its prefix and title. The Heilsumynd bot wires it into adminMenu.html `lessons` and deploys it.
- Source: screenshots and notes from the Heilsumynd bot in /shared/educator/heilsumynd/admin-<page>/. Its dev server is not reachable from this container.
- Order: Calendar, Customers (+refund), Products (+rules), Marketing/Póstar/Sendingar, Mail, Shipments, Visits, Gervigreind, Overview, Locations, sysadmin. Reception (/mottaka) is the nurse screen and is in Icelandic: ask Ingi.

## Hópkaup
- In Icelandic, because Hópkaup's admin UI is in Icelandic. The English rule covers Heilsumynd; Ingi can override.
- Page list: /shared/hopkaup/kennsla/admin-sidur.md (1 CRM, 2 Skrá vöru, 3 Breyta vöru, 4 Markpóstur, ...). Prófkóðar are excluded unless Ingi asks.
- Folders: hjalp/<sida>/<kafli>/. CRM moves to hjalp/crm/, and kaflar.js mappa paths follow it. Bút ids are unique across pages (vorur-01, ...).
- Live-UI tour, kafli.json + bút audio, as in /shared/hopkaup/hjalp/snid.md.
- Source: page list and descriptions from the hopkaup bot in /shared/hopkaup/kennsla/admin-<page>.md. Nothing invented.

## Limits
- Gemini TTS: 100 clips a day on the Hópkaup key. Both projects share it.
- Every lesson is tested in Chromium (1280x800 and the narrow slider) before handover.

Blocked on: screenshots and notes (Heilsumynd), and page descriptions (hopkaup).

## Progress 2026-11-03
- Hópkaup: vorur-01-skra, vorur-02-breyta, vorur-03-texti (sida "vorur") and CRM 07, 09, 10, 15 are written, voiced and checked; lok-01 now lists tilboðin. All chapters carry "sida". Sample product page: opna "vara-synishorn" (hopkaup bot built it).
- Heilsumynd: 3-calendar is done and tested in the dev slider by the Heilsumynd bot. 4-customers (step 12 has no audio), 5-products and 6-upsell are built and checked in Chromium; their audio waits for tomorrow's TTS quota.
- Waiting with screenshots: marketing, shipments, visits, locations, overview. Still to come: mail, gervigreind, sysadmin.
- Helpers: scratchpad make_lesson.py (template = 2-tests) and shoot.js (Chromium 420 px per step).

## 2026-11-04
- TTS tone direction (Ingi approved after a before/after test): `instructions` field in both tal.py; cache keyed on text only, so only new or changed búts get it.
- Heilsumynd customer help 16-minar-sidur (Icelandic, 12 steps) built from the Heilsumynd bot's test-user screenshots; voiced, transcription-checked, stepped through in Chromium; handed to the Heilsumynd bot to wire.
- Hópkaup rename (Hugmynd / Uppkast / Vara): on hold until Ingi approves; affected búts listed to the hopkaup bot.
