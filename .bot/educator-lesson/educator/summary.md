# Educator — summary

**Version:** v5

## What this is
Ingi asked (through the plang-me and peer bots, 2026-11-03) for in-app help on every admin page of two customer projects:
- **Hópkaup**: a live tour in Icelandic. It drives the real UI while Puck speaks, one short audio "bút" per sentence.
- **Heilsumynd**: step lessons in English in the "?" slider. Each step is a cropped screenshot with a highlight and voice.

Both projects are confidential. Only script text goes to Google TTS, and the images use only fake data.

## What was done
- **Hópkaup** (`/shared/hopkaup/hjalp/`, scripts in `tools/kaflar.js`, then `node tools/smida.js` and `python3 tools/tal.py <mappa>`):
  - Audio is finished for all CRM chapters.
  - New CRM chapters 07 póstsamskipti, 09 tilboð, 10 tilboð sem renna út and 15 tvítekningar, written from the hopkaup bot's descriptions.
  - New product chapters vorur-01-skra, vorur-02-breyta and vorur-03-texti. They use the sample page `opna: vara-synishorn`.
  - Every kafli.json now carries `sida`.
  - Chapters 08 and 13 stay off prod until mail sending is connected.
- **Heilsumynd** (`/shared/educator/animations/heilsumynd-admin/`):
  - New lessons 3-calendar to 14-ai-usage, built from the bot's screenshots and notes. Each was built from 2-tests as template and checked in Chromium at 420 px.
  - 3-calendar is voiced and in the Heilsumynd repo.
  - The rest wait for the daily TTS quota (100 clips per day). A background script retries every 30 minutes.
- **Next:**
  - Once the audio is in, tell the Heilsumynd bot which lessons can deploy.
  - Have someone listen to the flagged words. Hópkaup: bíð svars, Seldu, Á dagskrá, þemadagana. Heilsumynd: the Icelandic labels Puck reads in English text.

## Code example
A Heilsumynd step (in index.html):
```js
{ img: '08.png', hl: [391, 26, 266, 50], tap: true, title: 'Move a booking',
  text: 'To move a booking, call the customer first. Then pick a free slot in Move to, and press Move. ...',
  notes: '05: form in bookingCard.html, posts to /admin/calendar/day/move ...' },
```
A Hópkaup bút (in kaflar.js):
```js
['renn-04', 'Takkarnir +1, +7, +14 og +30 setja næsta samband ...', [{ blikka: 'rennur-ut.naest' }], [spurningar], [ord]],
```
