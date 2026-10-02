# builder — summary

## Version
v3 (2026-10-02) — architect's `builder-next-session.md` queue. Continuation of the v2 bug-fixing
round (issue queue `.bot/app-systems/architect/builder-issues-2026-10-01.md`). v1 = docs pass,
v2 = the bulk of the bug fixes (see v2/).

## What this is
The builder owns `os/system/**` (goals, `.llm` prompts, templates, teaching under
`os/system/modules/**`) and the module C# under `PLang/app/module/**`. Core (`goal/**`, `type/**`,
`event`, `actor`) is the coder's, through the architect. Other bots report mapping bugs; builder
diagnoses each against the PLang-written builder and either fixes the builder's own source or shapes
a core fix for the architect/coder.

## Decider key (resolved mid-session)
Early on, builds 403'd at `llm.decider` — no TypeSafe key in env/settings. Ingi then pointed me at
`/shared/hopkaup/secrets/typesafe.txt`; passing it as `TYPESAFE_API_KEY` (the env source
`TypeSafe.Config` already reads) unblocks builds. **It is NOT yet stored in a settings table** — I
pass it per-build via env. Ingi's intent is that it live in the settings table so the decider gets
it there; that storage step is still owed (needs a plang `set %!decider.apiKey%` into the build
folder's/os settings, or an env export in the bot runner).

## What was done (v3)
Full detail: `v3/result.md`. Shapes relayed in `to-architect.md` (v3 section). All diagnoses are
**static** (reading pick/decider source) — reliable for which-branch questions, but the counts that
confirm a fix still need the key.

- **Issue 5 (stray `.goal2`) — DONE.** `git rm os/system/Build_dpricated.goal2` (v0.1 builder
  sketch, non-`.goal` ext, only `.bot` referenced it).
- **Issue 25 (C4) — MEASURED 10/10 PRESENT on head; symptom resolved.** c4-1/c4-2 fresh, cache off,
  `Template=plang` 5/5 each. The Compile user message shows `file.read 0.98` (Certain) → Prefill
  fills `file.read(Path, Template=plang)` into `=> formal:`, the writer copies it. The educator's 4/6
  was on `aa9cadcd5` (file.read under 0.90 there). My static mechanism diagnosis holds (chosen option
  reaches the formal only via `Call()`←`Prefill()` over `Mark.Certain`), so the `listed.Option` shape
  is a **latent robustness** fix for an uncertain load-vars read — architect's call whether to still
  land it. Shape in `v3/result.md`/`to-architect.md`: coder adds `listed.Option` (pick is core),
  builder renders it on `=> decider:` (`properties.template` l.40, prepared).
- **Issue 2 with key — MEASURED, REPRODUCES: Item+Key present 3/5, both dropped 2/5** (fresh, cache
  off). Drives **Option-question v2** — drafted in `v3/option-v2-shape.md` (the architect asked for
  it; they review before the coder builds core). v2: a non-choice option (`Item`/`Key`/`Conversation`)
  offers the step's own variables + "none"; Prefill writes the pick. Needs `ask:` note lines on
  loop.foreach's Item/Key + llm's Conversation (builder's); the Conversation `{continue: …}` wrapper
  is an open question for the architect.
- **Issue 32(b) — MEASURED, REPRODUCES 5/5.** `call goal Page module=%!app.module.file%` →
  `goal.call(Name="%!app.module.file%")`, `Page` lost. goal.call's note is **already correct**; the
  *writer* misreads the dotted `%!a.b.c%` variable as the name. Evidence for the architect's
  decider/writer-opaque-variable direction (they own it); hold per their instruction.
- **Issue 30 pass 2 — DONE (closed by architect).** 15 remaining os/ goals; all build clean / have
  current valid .pr; no writer-mis-map/core/write-in-formal refusals left (v2-era failures were in the
  24 swept goals). 5 rebuilt this session (dropped deprecated isSetup/etc. per 819f239c6; kept). Two
  needed one FixSteps retry (issue-17 class, recovered). Table in `v3/result.md`.
- **`cache:false` regression — builder half FIXED + validated (`v3/cache-false-diagnosis.md`).** On
  head, cache:false stopped rebuilding an unchanged goal (reproduced on hash-take). Root: the CLI build
  setting wasn't visible as `%!build.setting.cache%` at Build.goal start, so `Build.goal:7`
  `set default … = true` clobbered it → `Default.cs:116` merged → `IsCached` (`goal/this.cs:288`) →
  skip. **Fix (architect-directed, my lane): deleted `Build.goal:7`** — the setting class already owns
  the default (`build/setting/this.cs:10` `Cache = true`), so the goal line was a redundant clobber.
  Rebuilt Build.goal's `.pr` (bootstrap, cwd=os/). **Validated:** hash-take unchanged cache:false now
  rebuilds ("Building goal: Start / Saved 8.0s", `.pr` mtime changes; md5 identical = deterministic,
  correct). **Wire: 9 failed / 480 passed — all 9 are baseline, zero new.** Core half still owed
  (coder): a setting must never read undefined when the CLI wrote it (the `.setting` projection,
  7c98a5e44 family). Fresh-folder measurements unaffected (no .pr to merge) — c4/loop/modules stand.
- **Issue 32 masking shape drafted (`v3/issue32-masking-shape.md`).** Step variables → opaque `%vN%`
  for both decider and writer (via a new `s.Masked` + reverse map on step, core); templates read
  `s.Masked`; the formal answer's `%vN%` maps back before parse. Couples with Option-v2 (shared mask).
  I own the template side + measurements; core to the coder after architect review.
- **Item 6** (goal.call `Parallel`/task teaching) — gated on the coder's stages 1–2 of
  `test/plan/task/`, not started.

## Key pattern (teach the writer, not the dev's goal)
The Properties writer reads an action's **notes** and the decider's `=> decider:`/`=> formal:` lines.
A param the writer must set from a droppable trigger word (load-vars→Template, `with key`→Key,
"continue"→Conversation) is unreliable by teaching alone — the fix is the decider surfacing the
value so the writer copies it. Issue 25's remaining gap: the surfaced value reaches the writer only
when the action is *certain*; a listed-but-uncertain action needs it too (the v3 shape).

## on.event slot `app.event`→`event` on two test .pr (architect task) — 1 done, 1 held
43441c5c9 renamed the event type's PLang word to `event`. Caught a **stale binary** first (built before
43441c5c9 entered my tree via rebase → emitted `app.event` + a phantom goal.call-name mis-map); clean
`dotnet build PlangConsole` fixed both. Then rebuilt each alone (cache off, cwd=test/):
- **CreateFiresOnBirth (file): DONE** — `event`, test Pass, committed. (Build flaky on issue-17 class;
  saved on retry.)
- **AsPathIsABirth (path): HELD at committed `app.event` (test green).** Rebuilding to `event` fails two
  ways, both core/out-of-lane: (1) `set %p% = "a.txt" as path` drops `Type=path` ~3/5 (the writer loses
  the `as path` coercion — the droppable-trigger-word / Option-question class; no `as path` example, and
  examples don't reach the writer anyway); (2) even with `Type=path`, runtime fails `'app.event.on.create'
  has no wire contract` — the path create event has no `[Out]/[Store]` face, a gap the rename surfaced
  (file.on.create has one). Handed to architect/coder. Detail in `v3/result.md`.

## Python decider validation retired (Ingi, via architect) — DONE
Deleted `tools/decider/` (the whole Python eval: harness.py, prompt_c.py, build_pr.py, formal.py,
runs/labels/out fixtures — 5442 files). `PickListTests` was already a pure C# golden test (reads
`pick_golden.json`, re-pins from C#); scrubbed the "a twin test holds the two equal" / `tools/decider/
harness.py` provenance from the six decider templates' `{% comment %}` blocks (comment-only, no
re-pin — goldens unchanged) and `Decide.code.md`. Deleted the dead `BootstrapTests.cs` (the Python-twin
judge, already `[Skip]`); its only shared helper `RepoRoot()` was byte-identical to `Fixture.Root()`, so
`BuilderPinTests`/`FormalStepTests` repointed there. Removed `[Arguments("tools/decider")]` from
`StartMdTests` and the `source_fix_check.py` comment in `ElseWithoutIfTests`. Character-memory +
`Documentation/` mentions → proposal (`claude-md-proposals.md`, builder v3), not edited (others' files).
**Wire: 480 pass (unchanged), 9 fail (baseline), no decider golden regressed.** One commit.

## Status 2026-10-02 (end) — idle, waiting on the coder's one pick pass
All architect-directed work done and accepted (latest 0b799a5d1). Open items are the coder's / core:
- **Coder's pick pass** (Option-v2 core + `listed.Option` + issue-32 masking core, after task 2b).
  When its hash lands: I add the `ask:` note lines (loop.foreach Item/Key, llm Conversation), switch
  the four LLM-facing templates to `s.Masked`, the `=> formal:` masking, then measure (issue 2 → 5/5,
  the control set, issue 33's `as path`).
- **Coder's stage 2b — shortcut .pr rebuild (queued by architect, gated on the 2b hash).** 2b changes
  `os/system/shortcut/goal.goal` and `step.goal` to read `%!app.call.scope.caller.goal%` / `…step%`
  (the call stack becomes the `call` concept). Their `.pr` still hold `%!app.callstack…%`, so `%!goal%`,
  `%!step%`, `%!where%` fail at run until rebuilt. **When the 2b hash arrives:** pull it; rebuild
  `os/system/shortcut/goal.goal` and `os/system/shortcut/step.goal` **each alone, cache off, cwd=os/**
  (bootstrap stamps — pre-check paths); audit each diff; run their readers (the ~6 C# shortcut tests
  the coder names + any plang test); push. **The architect gates at my commit together with the coder's**
  — so commit, don't expect a separate gate. (Mind the stale-binary trap: `dotnet build PlangConsole`
  after pulling 2b's C# before rebuilding.)
- **Issue 33 (new, logged by architect):** `set %p% = "a.txt" as path` drops `Type=path` ~3/5 — the
  drop class of 25 & 28 (a droppable coercion trigger word). Likely lever: the **Option question with
  type names as its offers**. Waits for the coder's pick pass. (Surfaced via the AsPathIsABirth .pr,
  held green at `app.event` until this + the path-create wire-contract fix land.)
- **Core, with the coder:** the `.setting` projection (`%!build.setting.cache%` reading undefined —
  cache:false regression root), `app.event.on.create` wire contract (path create event), and the
  pick-pass core.
- **With Ingi:** where the decider key lives (settings scope) — still env-only, not written to any store.

## Next session (in order)
1. **Decider key:** pass `TYPESAFE_API_KEY="$(cat /shared/hopkaup/secrets/typesafe.txt)"` to every
   `plang build`; still owed — store it in a settings table so the runner doesn't need the env each time.
2. Option v2 (`v3/option-v2-shape.md`): once the architect reviews and the coder builds the core,
   add the `ask:` note lines (loop.foreach Item/Key; llm Conversation) and measure issue 2 at 5/5.
3. Issue 25 `listed.Option` (if architect still wants it): apply the `properties.template` render
   after the coder lands the field; re-measure a *low-score* load-vars read (C4 itself is 10/10 now).
4. Issue 32: coder builds the opaque-variable core (step's variables → opaque placeholders for both
   decider and writer); I own the template side when the architect sends the shape.
5. Issue 30 pass 2: DONE (all current/clean). Architect to decide if a forced fresh rebuild of the 10
   skipped goals is wanted (needs fresh .build, off-limits for os/).
6. Item 6 after the coder's `test/plan/task/` stages 1–2 (coder v19 plan just landed).
