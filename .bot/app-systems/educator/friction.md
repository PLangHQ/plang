# Friction: the educator's review of the plang builder

A user's review: I write more plang than anyone, the way a learner would, for the course
(`/shared/educator/course/`, 20 lessons). Each entry: **what**, **cost** (the step, the build, the evidence),
**if I had** (the wish). Newest at the bottom of each section. Started 2026-10-02.

## Steps a learner writes that the builder misread

- **educator · the same step builds differently from build to build.** Every claim in a lesson needs several fresh
  builds, because one build proves nothing. **Cost:** C4 `read 'receipt.txt', load vars, write to %receipt%` went
  6/6 → 4/6 → 6/6 across three commits. `foreach %person% as %value% with key %field%, call …` dropped both names 1/6.
  `hash "…" with sha256` lost `Algorithm` in the studio. The rule "a sample is a fresh folder, count to 5" now costs
  me ~10 builds per lesson step that matters. **If I had** `plang build --samples=5` (build N times fresh, report which
  steps disagree and how), a lesson could claim "builds the same 5/5" in one command, and so could every test.
- **educator · the goal's comments steer the build.** **Cost:** `read 'note.txt', load vars` kept the option 3/3 when
  the goal had a `/` comment explaining load vars, and 0/3 without it (work/loadvars-probe vs loadvars-nocomment).
  A probe with an explaining comment "proved" a step the comment-free lesson failed. **If I had** comments reach the
  writer only as background (or be clearly marked as the author's notes, never as instructions), a probe would be
  a fair test of the lesson.
- **educator · `call goal Page module=%!app.module.file%` calls a goal named `%!app.module.file%`.** **Cost:**
  docs/Modules.goal can't be run: 0/8 right on head after df47eb144 (issue 32b). Traced: the writer answers
  `goal.call(Name="Page", module=…)`; the refusal says only "no property module"; the retry drops the argument. The
  signature reads `Parameter?: list` while every example writes a dict. **If I had** the type agree with the notation,
  and a refusal that says the fix ("an argument goes in Parameter={module: …}"), the retry would land.
- **educator · a word inside a variable pulls in its module.** **Cost:** `call goal Page module=%!app.module.condition%`
  got a junk `condition.compare(%!data% == %!data%)` (issue 32a). **If I had** the decider read `%…%` as opaque, a
  variable's name could never pick an action.
- **educator · `add "<text with %vars%>" to list %x%` keeps the template raw.** **Cost:** the studio put the literal
  `%lesson.examples%/%rel%` on a lesson page; a `set` first is the workaround (FINDINGS 36). Every other place renders
  where written. **If I had** the same rule everywhere, I'd stop testing which actions honour it.
- **educator · the module catalog teaches a step that fails.** **Cost:** `foreach %rows%, write out %row%` was in
  foreach.examples.md and on the learner page; it fails "%row% is not set" (fixed ba1969212). **If I had** every
  `*.examples.md` step built and run by the gate, examples couldn't teach what doesn't work.

## Errors a learner meets

- **educator · an error that blames the wrong thing.** `CreateItemDeclined (400): %Name% holds a module — a goal is
  read from its .pr, never converted from a value.` **Cost:** the step was right; the builder had put the module in
  the goal's name. A learner would rewrite a correct step. **If I had** errors that show the compiled action next to
  the step ("this step became goal.call(Name=%!app.module.file%)"), the learner would see it's a misbuild.
- **educator · what works:** `VariableNotFound … Check that it was assigned before this point, or that the dot-path
  navigation matches the value's actual shape`, with `at /Start.goal:6` and the step text. It says what to do; a
  learner can act on it. More of these.

## Seeing what plang wrote

- **educator · no plain view of a `.pr`.** Every lesson has a "what plang wrote" scene: step → `module.action  Prop: value`.
  I build it by hand from JSON for each lesson (python/node, because hooks block grep on `.pr` paths). **Cost:** ~15 min
  per lesson, and a reader of the docs can't do it at all. **If I had** `plang show Start` printing each step and its
  actions in one line each (the formal notation the writer already speaks), it would be the best teaching tool plang
  has: the build/run line made visible.
- **educator · LLM tracing also dumps the builder's own step debug.** **Cost:** tracing one 3-step goal gave 885 KB,
  with the 6 LLM blocks somewhere inside. **If I had** `--debug={"llm":…}` alone not switch on step debugging of
  the builder's goals, the trace would be the LLM exchange and nothing else.
- **educator · whether a build happened.** `plang build` with nothing changed prints "Found 1 goals" and stops; for
  a while cache-off did the same (fixed 414a1cfa3), and I mis-reported it. **If I had** a closing line ("built 1,
  unchanged 2, from cache 0"), no one would have to read `.pr` mtimes.

## Running

- **educator · permission prompts one file at a time.** After the permission store moved, the studio (which reads
  course files outside its root) asked once per file. Piped answers only count once per run, so it took 22 runs for
  the course page and 9 for the thumbnails. **If I had** an answer that grants the folder ("a" for
  `//shared/educator/course/`), or `--allow=read://shared/educator/course` for a run, one answer would do.
- **educator · a builder merge silently changes a working app.** **Cost:** rebuilding the studio after a merge turned
  its TTS `post … body {json}` into `http.upload` (issue 9) and dropped `with sha256` (issue 28). It failed only at run
  time, against a paid API. **If I had** `plang build --diff` (which steps' actions changed against the previous `.pr`),
  I'd see the regression before running it.

## What works well

- Plain steps for the everyday things (`add … to list`, `write out`, `save … to file`, `foreach … call`) build right
  every time and read like the lesson's sentence. 18 lessons are built on them.
- Errors carry `at <file>:<line>` and the step text, so a learner knows where.
- Templates render where they're written (1f496a715). That removed a whole class of surprises.
- The learner module pages generated from each action's notes can't drift from the code, and golden tests keep them honest.
- `--debug={"goal":"X"}` with BEFORE/AFTER variables made the studio's hash mismatch findable in two runs.
