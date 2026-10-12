# v3 — OBP lessons (the Object-Based Pattern)

## Ask (Ingi, 2026-10-01)
"I want a lesson about obp. you have docs about in the docs."
Answers to my two questions: audience = **the pattern, any language**; scope = **two lessons**.

## Source of truth
- `Documentation/v0.2/object_pattern_formal.md` — the 3 laws + the rules + why (the lesson's spine).
- `Documentation/v0.2/obp-smells.md` — the named smells, worked examples, meta-test (lesson B).
- `Documentation/v0.2/obp-scan.md`, `Documentation/Runtime2/obp-cleanup.md` — how smells are hunted; real backlog entries.
Docs lag the code: every code excerpt comes from a file at a commit, quoted verbatim with path:line in the
speaker notes. Doc pseudo-code is labeled "from the doc". Where the doc and the code disagree, the code is
shown and Ingi is told.

## Two languages
- **C#**: plang's runtime (`PLang/app/**`), the codebase the docs were written for.
- **JS**: the film engine (`/shared/educator/studio/film/**`), which I built as OBP (root `film.this`,
  context `film.frame.this`, sealed lesson data). Showing the same law in both is the "any language" proof.
- Honest: the film engine also breaks rules (`frame.scene = null` stamped later = *late stamp*; the scene list
  writes `start`/`end` onto scenes). Lesson A shows only clean parts; lesson B uses the breaks as examples.

## Lesson A — `03-obp-pattern` (~11 scenes)
1. Title.
2. The problem: friction (controller → service → repository; every layer knows the shape).
3. The insight: data flows blind; only the owner knows the shape.
4. Law 1 — the root (C# `app.@this`, JS `film.this`): everything by navigation.
5. Law 2 — the context belongs to the request (C# context, JS `film.frame.this` born per draw).
6. Law 3 — data flows through all methods (C# `Data`, JS lesson JSON sealed).
7. Lazy everything + no null checks (path.Size per the doc / a real lazy getter; JS `scene.example`).
8. The owner does the work; parents delegate (C# Load delegating; JS `film.draw` → `scene.draw`).
9. Navigate, don't pass (`Run(App app)` vs `Run(App, Channel, Cache)`).
10. The name is the contract + the three paths agree (plang / C# / file table; JS film namespace table).
11. Data rides sealed: the value does the work (`await A.Resize(Width, Height)`).
12. End: the laws are unbreakable, the rules bend with judgment.

## Lesson B — `04-obp-smells` (~10 scenes), after A
The smells by family (shape / value / design alarms), each with the doc's before/after, real hits where they
exist (obp-cleanup.md backlog, the film engine), and the meta-test.

## Order (per Ingi: content first, animation very last)
1. Collect and verify excerpts (C# via an Explore agent, verified by me; JS myself).
2. Write `/shared/educator/course/03-obp-pattern/lesson.md` (scenes, Say lines, excerpts, Why/source).
3. Show Ingi the lesson text before voicing.
4. Lesson JSON + art, voice, page, checks (shots, voices), mp4 export.
5. Then lesson B the same way.

## Open
- The doc's `app.Channel.Write(text)` and `path.Size` may be illustrative; verify against code.
