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
  correct). **Wire: 9 failed / 480 passed — all 9 are baseline, zero new.** CORRECTION (coder): the
  earlier "`%!build.setting.cache%` reads undefined / `.setting` projection bug" claim was based on a
  buggy debug watch (it reduced the watched var to its root and read only the store, `debug/this.cs:344,351`,
  so every watched setting showed "(undefined)"). The setting reads correctly (false with the flag);
  the only needed change was removing the redundant `set default` — no separate core bug owed. Fresh-
  folder measurements unaffected — c4/loop/modules stand.
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

## Pick-pass integration (coder 814ca5209) — builder half DONE + measured
Landed (d08a129df): four LLM-facing templates read `s.Mask.Text`; `properties.template` `=> decider:`
renders `listed.Option`; `ask:` lines on loop.foreach Item/Key + llm.query Conversation; `pick_golden`
re-pinned; Wire 487 pass / 9 baseline. Measurements (fresh, cache off, 5 each; table in `v3/result.md`,
commit 2f73f5479): **issue 2 Item+Key 5/5 FIXED, named continue 5/5 FIXED, plain-foreach/plain-read
guards hold.** ### DIRECTION CHANGE (Ingi, 2026-10-02): masking is a hack — REMOVED
Ingi ruled `step.Mask` out. The coder removes `step.Mask` + the restore (core); **I revert the four
templates from `s.Mask.Text` back to `s.Text`, in a commit paired with the coder's hash.** The `ask:`
lines (Option v2), `listed.Option` render, and the goal.call note all STAY (not masking).
**New lever — teach what `%!…%` is (FINAL wording, architect-approved).** Put BOTH sentences, once, in
`decider.state.template` (shared state) and on `Properties.llm`'s variable line:
> `%name%` is a variable — a value the step uses, never words of the step. `%!…%` reads a value from
> the app itself: `%!app.module.file%` is the file module as a value, `%!llm.setting.cache%` is the llm
> setting's cache option, `%!data%` is the result of the action before it. The words inside `%…%` are
> its name, never what the step does.
> In `call X name=value`, `name` is what the called goal reads the value as (`%name%`), even when it's
> a plang word such as `module`, `file` or `goal`; it is never a module or action of the step.

Then re-pin, Wire, and measure fresh, cache off, 5 each: **set A** (educator's exact `modules-nocomment`
goal, `module=`) **and set C** (`m=`) — to see if the teaching closes the 0/5 vs 4/5 gap — plus 32a
(condition step), control (`save %!llm.setting.cache%`), issue 2 (key), named continue, guards. One table.
**Pending the coder's Mask-removal hash** (template revert `s.Mask.Text`→`s.Text` + this teaching land
together in one commit paired with that hash).

**32(b) note-lever CORRECTED:** my earlier "4/5" did not reproduce — on the educator's exact goal it is
**0/5** (`module=`), matching their 0/8; a clean guards-goal run is 2/5; and `m=` instead of `module=`
is 4/5 — so the parameter *name* `module` (a plang concept echoing `app.module.file`) is the aggravator,
not something the note fixes. The goal-name offers (now folded into the no-masking plan via the `%!…%`
teaching + the `goal` type) are the real fix. Full table in `v3/result.md`.

### Edges resolved by the architect (core → coder batch; my ask: lines + re-measure follow) [SUPERSEDED by the no-masking direction above for the mask parts; ask: lines + offers still stand]
1. **Control regression →** do NOT exempt `%!…%` paths (that revives 32a). Instead **the mask carries
   each variable's type** where the build knows it: `save %v1% (setting)`, `call goal Page module=%v1%
   (module)` — same annotation `write to` already gets (`%x% (hash)`). The type is the honest signal,
   its words aren't. Core = Mask; **my part = templates** (render the annotation — confirm whether
   Mask.Text already embeds it or the template adds it, when the core lands).
2. **32(b) →** the **goal type** answers its offers = the app's goal names reachable from the step's
   goal. Core = goal type `Offers(step)`; **my part = an `ask:` line on `goal.call.Name`** (decider
   picks `Page` from reachable goals, not the step's variables).
3. **33 →** same: the **type** type answers its offers = plang type names. Core = type type `Offers`;
   **my part = an `ask:` line on `variable.set.Type`**.
4. **Bare-continue flake →** an option's offers never include the step's own `write to` destination
   (the pick knows it via `Destination()`). Core only.
Order: coder lands 1/2/3 core + 4 as one batch after its current queue → I add the ask: lines +
template annotation → **re-measure all eight scenarios**. Idle until that core hash.

### (original, now resolved) Open edges sent to the architect: (a) **control regression** — `save %!llm.setting.cache%`
masks to `save %v1%`, loses the `setting` signal → file.save mis-map, 0/5 (masking cost for
`%!…setting…%` steps); (b) **32(b) not fixed** — the writer still picks the variable as goal Name over
`Page` even masked (32(a) decider half IS fixed); (c) **33** — Option question doesn't reach `set.Type`
(no ask:, offers would be type-names not placeholders); (d) bare-continue guard flaky 2/5. Awaiting the
architect's calls on these.

## Status (latest) — masking removed, %!…% teaching landed; 32a + control fixed
Masking removed (Ingi; coder 8a4ef5fc7, templates reverted to `s.Text`). My `%!…%` teaching landed
(47a568651) in `decider.state.template` + `Properties.llm`; re-pinned pick_golden; Wire 489/9 baseline.
Measured (fresh, cache off, 5 each; direct `.pr` inspection):
- **32(a) FIXED** — no condition.compare junk from `%!app.module.<m>%` (0/5).
- **control FIXED** — `save %!llm.setting.cache%` → setting.save 1.00 (was file.save under masking);
  residual refusal = the separate issue-17 class.
- **32(b) unchanged** — writer still picks the variable as goal `Name`; fix = the coder's goal-name
  offers (`goal` type `Offers(step)`, queued). Teaching = decider half; offers = writer half.
- issue 2 (Item+Key), named continue (Conversation), 3 guards all hold.
**Issue 34 shape** refined (generic Agree rule over all options; type answers how its offer enters the
line — no Kind.Presence/marker, no Call fork) → coder after the offers batch; my `ask:` line + teaching
land when the terminal module reaches app-systems.
**Issue 35:** not path normalization (path type preserves `//`); writer dropped the slash — architect
asked the os bot for the raw `.pr` `Program`.
**CLOSED this session:** 32(a) + control (decision 542, gate 94); **32(b) + 33** (offers d2aa1358d +
my `ask:` lines on goal.call Name / variable.set Type, b55b64358 — Name="Page" 4/5, Type="path" 5/5);
AsPathIsABirth `.pr` committed with the `event` slot (1e90307be — the wire-contract error no longer
reproduces on current core; verified Pass).
Idle pending:
- **Issue 34 (type-Example, architect-refined to core → coder):** `list<permission>` answers its own
  Example (from its element's), template unchanged. My part when it lands: measure a `list<T>` slot on
  app-systems whose element has an Example (find one / name the gap) + a `list<text>` guard.
- **Issue 34 (5-build) + 35 (10-build) measurements BLOCKED** — terminal module not on app-systems
  (`start //bin/sh …` / `list<permission>` unbuildable here). Run on plang-os-stable or once merged.
- **Coder follow-up incoming:** offers become items via `formal.Writer` — re-run the 32(b) guard after.
  (DONE 94d8ed814: nothing dropped, all 5 hold.)
- Terminal module → the 34 `ask:` line + teaching (shape ready).
- decider-key storage scope — Ingi.

### Issues logged 2026-10-02 (architect) + my re-measure triggers
- **36 — FIXED** (coder 220b07145/ae2bc8549; measured 5a586e25a): `foreach %x%, call Y y=%item%` →
  no `Key=%item%` 5/5; guards hold (`as %i%` → Item,no Key; `with key %field%` → Item+Key), 5/5 each.
- **37 — cast FIXED** (coder 6c136ec1a): no more `InvalidCastException: lower app.module into list`.
  32(b) edu now 9/10 `Name="Page"`.
- **39 (the ~1/10 non-recovery) — diagnosed, CORRECTED:** NOT a FixSteps set failure. The set
  succeeds and the retry `llm.query` runs; the retry's re-answer is `goal.call(Name="Page",
  Parameter=%!app.module.file%)` — the writer writes the Parameter as a **bare nameless value** (drops
  the arg name `module`), both initially and on retry, and does NOT copy the placeholder `name`. So
  build.match refuses again → NO PR. Fix (architect/coder): the refusal should name the step's own arg
  (`{module: …}`) not the placeholder `name`; and/or teach that a plang-word arg name is still a name.
  Same plang-word pull as 32(b), on the Parameter side.
  - **⚠️ Lesson (twice now):** the DEBUG `[BEFORE]`/watch `%x% = (undefined)` listing is UNRELIABLE
    (debug/this.cs:344,351 — it fooled me on cache:false AND here). **Never conclude a variable is
    undefined from the debug listing — check the step's DEBUG [AFTER] / the actual effect first.** My
    first issue-39 read ("set fails, retry never runs") was wrong because of this; corrected after
    reading the AFTER state (fixMessages populated, retry ran). [[feedback_use_debug_not_csharp]]
- **38** (1/5 `hash the file 'x.bin'` hashes the name's letters): waits on Ingi (hashing a path's content).
- **34 type-Example** (element-kind change): coder's. When it lands → measure a `list<T>` slot on
  app-systems whose element has an Example (+ `list<text>` guard).
- **34 (5-build) / 35 (10-build)**: blocked — terminal module not on app-systems.

## (earlier) Status 2026-10-02 — idle, waiting on the coder's one pick pass
All architect-directed work done and accepted (latest 0b799a5d1). Open items are the coder's / core:
- **Coder's pick pass** (Option-v2 core + `listed.Option` + issue-32 masking core; coder plan v22 =
  `1ea0f67bf`). When its hash lands, my part:
  - switch the four LLM-facing templates (decider1, decider2, decider.state, properties) from `s.Text`
    to **`s.Mask.Text`** (architect's confirmed member name — not `s.Masked`/`question.Variable` as my
    shape drafts guessed; see `v3/issue32-masking-shape.md` note);
  - add the `ask:` note lines on **loop.foreach's Item/Key** and **llm.query's Conversation**;
  - measurements, **in this order:** (1) the control set (a step whose only module signal is a
    variable name — masking must not regress it), (2) issue 2 with a key → Item+Key 5/5, (3) issue
    32(b), (4) the named "continue the conversation from %answer%", (5) the guards (plain foreach keeps
    Item/no stray Key; plain read stays Template-free; a bare continue → none).
  (2b's `AShortcut` stale test string was fixed by the coder in `156ed9e92` — accepted, off my plate.)
- **Coder's stage 2b — shortcut .pr rebuild — DONE (on 1dc8a4606).** Rebuilt the binary (2b is C#),
  then rebuilt `os/system/shortcut/goal.goal` and `step.goal` each alone, cache off, cwd=os/. Clean
  diffs: `%!app.callstack.scope.caller.{goal,step}%` → `%!app.call…%` (step text, value, variable text,
  the `property: "callstack"` → `"call"`), hash updated, nothing else moved (each after one
  self-recovering issue-17 retry). **Tests: SystemShortcutTests 5/5, ReturnTests 22/22, ShortcutTests
  5/6** — the four named tests pass (ReturnGoal, ReturnStep, TheCallersData, TheGoalAndStep).
  **AShortcut_ReadsAsItsGoalsAnswer_ForItsAsker still fails — NOT my .pr:** the test itself hardcodes
  the old path at `ShortcutTests.cs:52` (`Shortcut("where", "%!app.callstack.scope.caller.goal%")`),
  which no longer resolves after 2b → `%!where%` holds nothing. That's the coder's test string to
  update (callstack→call); flagged to coder + architect, not edited (their test, parallel commit).
  Committed the two .pr for the architect to gate with the coder's 2b.
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
