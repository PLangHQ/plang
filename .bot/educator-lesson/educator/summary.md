# educator: summary

**Version:** v2, in progress (2026-09-30). v1 = lesson 1 "What is plang", done and voiced. v2 = lesson 2 "your first plang program" (hands-on), text first, animation last (Ingi).

## What this is

This work builds two things:
- **studio**, an OBP animation library for PLang teaching animations, in `/shared/educator/studio`.
- **"What is plang"**, a beginner lesson made with it.

The three bots aniva, rafbokin and coder had each written the same canvas engine, with timings copied by hand and state shared through globals. studio is the one shared version.

The split is Ingi's call:
- PLang does the building: voicing, and writing the page.
- JavaScript is only the in-browser engine.
- Playwright is only used for checking.

## State

**Engine:** `studio/film/**`, one folder per concept, `this.js` each.
- The root is `film.this`, the context is `film.frame.this`, and the lesson data is sealed.
- Every method carries an `origin:` comment pointing to the original it came from; `REFERENCE.md` indexes them.
- Voice lines measure their own duration from their PCM, and the scene list lays out the timeline once. No time is written by hand.
- Sound is scheduled live from the play position. An up-front offline render took 2–3 minutes for 90 s in the check container.

**PLang app** (`studio/*.goal`):
- `Start` voices every line and writes the page. It is **blocked** by an http bug on app-systems.
- `Draft` writes the page without voice. It works.
- `voice/Key`, `voice/Scene` and `voice/Line` handle voicing; each take is cached per line id with a hash of what made it.
- `page/Write` inlines the engine, fonts, lesson and art through `ui.render`.

**Brand (Ingi: keep coder's logo.png and plang logo):** `film.puffin` (logo.png cut into a paper puppet), `film.logo` (ransom-letter wordmark, teal sunburst card), `film.image` (inlined pictures). Used in the title card, the hook, the run chapter and the end card.

**Music:** tuned by listening through Gemini (`studio/check/listen.js`): the bass and brush layer read as "groovy electronic", now level 4 and unused; levels 1-3 rate 10/10 warm and storybook-like.

**The lesson:**
- Script: `studio/lesson/what-is-plang/lesson.json` (9 chapters, 15 lines, every claim with its source in the speaker notes).
- Drawings: `studio/lesson/what-is-plang/art.js`.
- Written by `plang Draft topic=what-is-plang` to `/shared/educator/animations/what-is-plang/index.html`.
- Checked with `node studio/check/shots.js what-is-plang` (24 shots, no errors).

**Facts verified on this branch:**
- The example goal (`/shared/educator/work/hello`) builds and prints "Hello, world!".
- It runs with every LLM key removed from the environment.

**Blocked:**
- The http bugs (FINDINGS 9, 18, 19) are reported to the architect (`plang-21`), who passed them to the coder.
- The coder will tell educator to pull `app-systems` when fixed. Then run `plang Start topic=what-is-plang`, check the takes by transcription, and re-shoot.

**Open for Ingi:**
- Which install and command lines the "Your turn" chapter shows. The latest release is v0.1.17.3 (`plang exec`); this branch uses `plang build` then `plang`.
- Listen to the music.

## Files

- `/shared/educator/studio/FINDINGS.md`: 20 PLang behaviours that contradict the docs, or are bugs, each with a repro in `/shared/educator/work/probe/`.
- `/shared/architect/educator-http-body.md`: the report for the architect.

## Code example

```js
film.scene.art.variable = frame => {
  frame.code('goal', GOAL, { x, y, size, w: 880, title: 'Start.goal', focus: second ? [2] : [1] });
  const tagIn = word(frame, 0, 0.55);   // a beat placed at 55% of voice line 0
  tag.draw(frame, right + 330, ty, { scale: frame.at(tagIn, tagIn + 0.45, 'outBack') });
};
```

## v2: lesson 2 (hands-on)
- The lesson text is `/shared/educator/course/02-first-program/lesson.md`, with its examples as real goals in `code/NN-*/Start.goal`. Each is built and run on this branch; real output in `output.txt`.
- 01 hello, 02 variable, 05 list/foreach, 07 goal with a value and 08 shopping list work.
- 03 ask, 04 condition and 06 file read are blocked by PLang bugs, reported in `/shared/architect/educator-beginner-steps.md`. The architect has queued them after http 2 and 3.
- Install and editor screens and facts were asked of the os bot (`/shared/educator/os/first-program/`, stills and videos). Nothing has arrived yet.
- Engine: `film.video` and `film.paper.photo` are started but not wired or tested. They are parked until the lesson text is settled.
