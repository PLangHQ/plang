# educator: summary

**Version:** v1, in progress; interrupted by a WSL restart on 2026-09-30.

## What this is

This work builds an OBP animation library, **studio**, for PLang teaching animations, and then a beginner lesson, "What is plang", made with it. Three bots (aniva, rafbokin, coder) had each written the same canvas engine with copied timings and globals. studio is the one shared version.

The split is Ingi's call:
- **PLang does the building:** voice, timings, writing the page.
- **JavaScript is only the in-browser engine.**
- **Playwright is only for checking.**

## What was done

- **Engine:** `/shared/educator/studio/film/**`, one folder per concept, `this.js` each (`film.scene.list.this` is `film/scene/list/this.js`).
  - The root is `film.this`, the context is `film.frame.this`, and the lesson data is sealed.
  - Every method carries an `origin:` comment pointing to the original.
  - The index is `/shared/educator/studio/REFERENCE.md`.
- **Engine test:** smoke-tested in chromium with the real voice, with no errors. The voice is intelligible over the music (checked by transcription), and the peak is −0.5 dBFS. The test page is `/shared/educator/animations/_engine-test/index.html`.
- **Other files:**
  - page template: `/shared/educator/studio/page/index.html`
  - lesson format: `/shared/educator/studio/lesson/README.md`
- **Voice:** Achird on `google/gemini-3.8-flash-lite-tts`, through OpenRouter `/audio/speech`.
  - The output is PCM only.
  - Direction goes in `instructions`.
  - The name is written lowercase "plang".
  - Samples are in `/shared/educator/voices/`.
- **PLang probes:** results, and the contradictions to report, are in `/shared/educator/studio/FINDINGS.md`. The probe goals are in `/shared/educator/work/probe/`.

## Next

1. Solve the byte count, which gives each line's duration (see FINDINGS "Open").
2. Write the studio goals: `Start`, `voice/Speak` (with a transcription check), `voice/Time`, `page/Write` (via `ui.render`).
3. Write the "What is plang" lesson script and art, trace every claim, and build and check at 1280×800.

## Code example

```js
film.scene.art.hook = frame => {
  new film.paper.piece.this({ w: 600, h: 360, seed: 'note' })
    .draw(frame, 960, 500, { scale: frame.at(0.2, 0.8, 'outBack') });
  new film.text.this('Just write what you want', { size: 76 })
    .draw(frame, 700, 510, frame.on(0, 0.3, 2.0));   // writes on with voice line 0
};
```
