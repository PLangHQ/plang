# educator: summary

**Version:** v3 (2026-10-01). v1 = lesson 1 "What is plang". v2 = lesson 2 "Your first plang program". v3 = lesson 3 "OBP, the object-based pattern", the mp4 export, and the course web page. All three lessons are voiced, checked, exported to mp4 and on the course page.

## What this is

Teaching lessons for developers new to plang, written as narrated, hand-drawn animations:
- **studio** (`/shared/educator/studio`): an OBP animation library plus a PLang app that voices a lesson and writes its page. PLang does the building; JS is the in-browser engine; Node + Playwright only check and export.
- **course** (`/shared/educator/course/<lesson>/`): each lesson as `lesson.md` plus code that was built and run for real; the animation is made last, from it.
- **the course page** (`/shared/educator/index.html`, Windows `C:\Dev\claude\shared\educator\index.html`): every exported lesson as a card; each lesson's mp4 with chapters (and their sources) and the narration as a transcript that follows the video.

## State

| Lesson | Course text | Animation | mp4 |
|---|---|---|---|
| 1 What is plang | `course/01-what-is-plang` | `animations/what-is-plang` | 98.7 s |
| 2 Your first plang program | `course/02-first-program` | `animations/first-program` | 206.5 s (Writer build-and-run screens still pending Ingi) |
| 3 OBP, the object-based pattern | `course/03-obp-pattern` | `animations/obp-pattern` | 231.4 s |
| Advanced: OBP in C# | `course/obp-in-csharp` (C# programs run with `dotnet run`) | not animated | — |

**Pipeline for a lesson** (`studio/`):
1. `lesson/<topic>/lesson.json` (scenes, voice lines, cues, source notes) + `art.js` (one drawing per scene).
2. `plang Draft topic=<topic>` writes the page with estimated timings; `plang Start topic=<topic>` voices every line (Gemini TTS via OpenRouter, cached by hash) and writes the page.
3. Check: `node check/voices.js <topic>` (every take transcribed against its line; `check/ask.js` for targeted questions), `node check/shots.js <topic>` (3 shots a chapter), `node check/listen.js <topic>`.
4. `node export/mp4.js <topic>`: 720p, `--disable-gpu`, 3 browsers (the benchmark winner, `export/bench.js`), writes `<topic>.mp4`, `<topic>.timeline.json` and `<topic>.jpg`.
5. `plang Hub` writes the course page from the timelines.

**v3 decisions (Ingi):**
- OBP lesson: the pattern, any language; two lessons (laws + rules, then the smells). Then: C# only, simpler, problem first; then move the C# out as an advanced lesson; lesson 3 = the laws lightly, the root in plang (`%!app.type%`), the rules, breaking a rule as a recorded exception; then add the new dot-case rule ("a name is a path") with a real `.pr`.
- Export to mp4 because the HTML pages didn't play in Ingi's browser; then a web UI over the lessons.

**Open:**
- Lesson 4, the smells (`obp-smells.md`), not started.
- The OBP doc's examples differ from the code in six places (table at the end of `course/obp-in-csharp/lesson.md` and the previous lesson 3 text); not yet sent to the docs bot (asked Ingi).
- `%!app.product%` (an app's own concepts on the root) isn't possible today; taught with plang's own concepts.
- FINDINGS 28-31 (`studio/FINDINGS.md`): piped permission answers reach only the first prompt; `%!app.goal.list%` resolve cycle; one "timeout" phrase compiling to two timeouts; "a this" in an error message.
- Doing the export in plang needs browser actions that `plang-os`'s browser module doesn't have yet (run JS, capture a frame now, launch flags); asked Ingi who adds them.

## Code example

A lesson scene places every beat on a word of its voice line, and draws cards with the engine's new `film.paper.label`:

```js
film.scene.art.root = frame => {
  label('app', { size: 96, color: P.sun, seed: 'app' }).draw(frame, 960, 250, { scale: frame.on(0, 0.4, 0.8, 'outBack') });
  const a0 = frame.word(2, 0.62);   // "like reading an address"
  arrow(frame, [940, 320], [670, 420], [390, 580], P.clay, frame.at(a0, a0 + 0.7), 'addr', 9);
};
```

## v3 after review

Ingi's review of the first lesson 3 text: verbatim production excerpts in two languages were noisy, and "I didn't understand why it's good that any object can have access to context". Response: one small teaching example shown failing first (a static user mixes two concurrent checkouts: `Ada: 4990 USD`, 5/5 runs), then the context fixing it, and the cost of one more need measured with a real diff (3 classes change vs 2). Later moved to the advanced lesson; lesson 3 now explains the laws in plain words.
