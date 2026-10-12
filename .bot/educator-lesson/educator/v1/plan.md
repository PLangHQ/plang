# v1 plan — `studio`: an OBP animation library, then a beginner PLang lesson

## Why

Three bots each wrote a hand-drawn canvas film engine: aniva (`/shared/aniva/animation`), rafbokin (`/shared/rafbokin/animation`), and coder (`/shared/coder/animation`, v1–v5). All three have the same core:

| concern | aniva | rafbokin | coder |
|---|---|---|---|
| frame = pure f(t) | `scenes.js:829 renderFrame` | `scenes.js:722 render` | `v5/js/scenes.js:315 renderFrame` |
| tween core (clamp/lerp/prog/E) | `engine.js:28-40` | `core.js:81-129` | `core.js:33-50` |
| seeded rng / hash | `engine.js:13-27` | `core.js:11-38` | `core.js:52-68` |
| boil (stop-motion reseed) | `engine.js:42-45` (12/s) | `ink.js:14-17` (8/s) | `core.js:8-10` (12/6 per s) |
| wobbly ink line | `engine.js:116 sketch` | `ink.js:119 ink.stroke` | `core.js:286 ink` |
| torn paper piece | `engine.js:163,222` | `paper.js:190,216` | `core.js:154,189` |
| tape / sticker / stamp | `engine.js:278,303`, `scenes.js:128` | `paper.js:297`, `draw.js:515` | `core.js:234,256`, `art.js:290` |
| handwriting reveal | `engine.js:337 hand` | `ink.js:348 ink.text` | `core.js:365 hand` |
| torn-edge scene wipe | `scenes.js:803,843` | cross-dissolve `scenes.js:750` | `scenes.js:289,315` |
| grain / vignette / camera drift / shake | `scenes.js:194,816` | `paper.js:174`, `scenes.js:739` | `scenes.js:295,301` |
| captions | `scenes.js:872-904` (canvas) | DOM `index.html:261` | `art.js:497`, `scenes.js:335` |
| synth score + SFX | `audio.js:22` (scheduled) | `audio.js:433,642` (lookahead) | `audio.js:27,286` (offline bake) |
| VO sync | `index.html:113` (setTimeout) | `index.html:230` (setTimeout) | `player.js:99-114` (audio-clock, sample exact) |
| player (play/seek/keys/`?t=`) | `index.html:57-150` | `index.html:141-415` | `player.js` |
| export (playwright → ffmpeg) | `render.js` | `tools/export.js` | `tools/export.js` |

**The duplication hurts:**
- Scene times are copied by hand into up to five places: scenes, foley, VO table, manifest, SRT.
- Everything talks through globals and script load order.
- The voice either drifts (setTimeout) or never reaches the export.
- None of the three ever produced a real voiceover.

## Shape (OBP: root, context, data)

**The split (Ingi, 2026-09-30):**
- PLang builds everything: script → voice → timings → bundling the page.
- The in-browser player/draw engine is JS, because the browser can't run PLang.
- Playwright is only used for checking.

### Data: `lesson`
One document describes a lesson: its scenes, and the voice lines inside each scene. Cues hang off lines, never off absolute seconds.

The PLang app creates it, stamps timings measured from the real voice audio, and embeds it in the page. From then on it flows sealed. The JS only reads the times PLang wrote.

**Timings live in one place:** the scene start is its first line's start, and captions *are* the lines.

### Root: `film` (JS)
There is one root. Everything is reachable from it, and there are no globals besides `window.film`:

```
film                      root, born from the lesson data
  .scene                  scene.list   — select by id, [i]; .current(t)
  .voice                  voice.list   — every line, audio clock; .current(t) → caption
  .cue                    cue.list     — sfx cues, resolved to seconds once
  .paper                  paper        — texture tiles + sprite cache (was the global caches)
  .sound                  sound        — graph, score, sfx kit, offline bounce
  .player                 player       — transport, keys, ?t= ?still= ?cc=, __film hooks for export
  .style                  style        — palette, fonts, post pass
```

### Context: `frame` (JS)
There is one context per render, the "request". It is born with `film`, `t`, `tick` (12 fps stop-motion), `boil`, and the canvas `g`.
- Randomness belongs to it (`frame.random(key)`), so reseeding the boil is no longer global mutable state.
- Scenes and drawables receive `frame` and navigate from it (`frame.film.paper`). No parameter lists.

### Values own their operations
- `line` (a polyline value) owns `resample`, `trim(p)`, `wobble(frame)` and `draw(frame, look)`. This is the ink stroke from all three engines.
- `piece` (torn paper) owns `bake` and `draw`. So do `tape`, `sticker`, `stamp` and `text` (handwriting with `reveal`).
- A scene holds its own draw logic, and the list only selects.

### The three paths agree
| lesson data | JS | file |
|---|---|---|
| `lesson.scene[2]` | `film.scene[2]`, class `scene` | `studio/film/scene/this.js` |
| `lesson.scene[2].voice[0]` | `film.scene[2].voice[0]` | `studio/film/voice/this.js` |
| — | `film.scene` (the list) | `studio/film/scene/list/this.js` |

The files are authored one concept per folder. They can't be ES modules, because `import` is blocked on `file://`, so PLang's bundle goal inlines them in dependency order into one `index.html`.

### Reference: jump to the owner
Every library method starts with a one-line origin comment naming the implementation it came from, for example:

```js
// origin: rafbokin/animation/src/ink.js:119 ink.stroke (resample→trim→wobble→trace), boil seed from coder core.js:286
```

`studio/REFERENCE.md` indexes concept → library owner → origin(s). That makes three jumps easy:
- from a lesson to the method that owns the behaviour,
- from the library back to the battle-tested original,
- from a bug in the original forward to the one place it is now fixed.

### Best-of choices (with the reason)

**Picture**
- **Ink line:** rafbokin's `ink.stroke`. The normal-direction fbm wobble with damped ends looks best. Its globalAlpha fix (child primitives multiply into the parent's alpha, so nested fades work) is carried everywhere.
- **Paper:** rafbokin's fibre tile, mirrored 2×2 so no seam shows, applied with `overlay` so one tile works on any colour. Torn pieces use coder's pale-core `tornRect` / `scrap`, baked once into sprites.
- **Text:** handwriting never boils, because text that shivers is unreadable (rafbokin `ink.js:344`).
- **Captions:** drawn on canvas, not in the DOM. This is aniva's approach: it exports correctly and the fade is kept.

**Sound**
- **Clock:** the audio clock is master, and it falls back to wall time only while the audio isn't running (rafbokin `A.running()`).
- **Voice:** voice lines are AudioBuffers scheduled on the audio clock and resumed mid-line after a seek (coder `player.js:99-114`). They are included in the offline bounce, so the export has the voice.
- **Randomness:** seeded everywhere, including the audio. All three still call `Math.random` in their audio code; this one won't.

## PLang app: `studio` (in `/shared/educator/studio/`)
```
Start.goal               build a lesson:  plang Start topic=what-is-plang
lesson/Load.goal         read lessons/<topic>/lesson.json
voice/Speak.goal         per line: cached by hash(text+voice), else http POST OpenRouter openai/gpt-audio, save
voice/Time.goal          durations → stamp line/scene start times into %lesson%
music/Compose.goal       optional: google/lyria-3 music bed, else the JS synth score
page/Write.goal          read studio/film files in order + lesson scenes + %lesson% + audio as data URIs → animations/<topic>/index.html
```

The page must work by double-clicking, so all audio is embedded as `data:` URIs. The API already returns base64, so PLang only concatenates strings and never decodes bytes.

## The lesson (after the library)
"What is PLang", for beginners, about 2½ min, 8 chapters (outline agreed with Ingi earlier today). It needs:
- chapter controls on top of timed playback,
- speaker notes (`N`) with source file:line for every claim,
- an example goal that I actually built and ran on `app-systems`.

## Steps
1. `studio/film/**`: port the core. Order: tween+random → frame → line/ink → paper/piece/tape/text → scene list + wipe → voice/caption → sound → player. Include origin comments.
2. `studio/*.goal`: build and run with `plang` from `app-systems`.
3. Test lesson (2 scenes, synth voice-free) to prove the pipeline, then check it with Playwright screenshots.
4. Real lesson once TTS works.
5. `REFERENCE.md`, update `animations/INDEX.md`.

## Voice (decided 2026-09-30)
- **Engine:** Gemini 3.8 TTS via OpenRouter `/api/v1/audio/speech` (the account has credits now).
- **Voice:** Achird on `google/gemini-3.8-flash-lite-tts`, chosen by Ingi (full and lite sound the same).
- **Output:** raw PCM, 24 kHz, 16-bit, mono. The runtime decodes it into an AudioBuffer itself.
- **Direction:** goes in the `instructions` field only.
- **The name:** the script writes lowercase "plang" (Ingi's choice; never "PLang", which gets spelled out as "P-Lang"), and the instructions say it's one syllable. Verified by transcription: 1 syllable.
- **Checking takes:** `voice/Speak.goal` should transcribe each new take back with `gemini-3.8-flash`. It checks the text and the syllable count before the take is accepted.

## Blockers / open
- **Builder key:** `TYPESAFE_API_KEY` from `/shared/hopkaup/secrets/typesafe.txt`, exported per command and never copied.
- **Fonts on `file://`:** they need to be inlined as data URIs. Can PLang base64 a binary file? I haven't verified this. The fallback is to keep pre-encoded font text files in `studio/font/`.
- **Branch:** `educator-lesson`, cut from `app-systems` (Ingi, 2026-09-30).
