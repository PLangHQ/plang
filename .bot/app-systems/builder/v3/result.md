# builder v3 — result (2026-10-02)

## ✅ UPDATE — decider key provisioned mid-session; measured (fresh, cache off)

Ingi pointed me at the key (`/shared/hopkaup/secrets/typesafe.txt`; passed as `TYPESAFE_API_KEY`,
the env source `TypeSafe.Config` already reads). Builds run now. **Measured results revise the
static diagnoses below:**

- **Issue 25 (C4): 10/10 PRESENT on head — symptom does NOT reproduce.** c4-1 5/5, c4-2 5/5,
  `Template=plang`. The Compile user message shows why:
  `[3] … => decider: file.read 0.98, variable.set 0.92 => formal: file.read(Path, Template=plang); …`
  On head `file.read` scores **0.98** (≥ Near 0.90) → **Certain** → Prefill fills `Template=plang`
  into `=> formal:` and the writer copies it. The educator's 4/6 was on `aa9cadcd5`, where file.read
  evidently scored under 0.90 on these folders. **The static mechanism diagnosis holds, but C4 is
  resolved on head.** My `listed.Option` shape is now a *latent robustness* fix (protects any
  load-vars read that scores under Near), not a C4 necessity — architect's call whether to still land it.
- **Issue 2 (loop with key): REPRODUCES — Item+Key present 3/5, both dropped 2/5** (fresh, cache off,
  `/shared/educator/work/guide-examples/runs/loop-dict-1`). Real flaky drop, both-or-neither.
  → drives the Option-v2 shape (`v3/option-v2-shape.md`).
- **Issue 32(b): REPRODUCES 5/5** — `call goal Page module=%!app.module.file%` →
  `goal.call(Name="%!app.module.file%")`, `Page` lost; the dotted variable takes the Name slot.
  **goal.call's note is already correct** (`call goal Page …` → Name="Page", Parameter={module:…});
  the *writer* misreads the `%!a.b.c%` variable as the name. (Repro: `/shared/educator/work/modules-probe`.)
  - **Cheap in-lane lever TRIED and FAILED (architect's suggestion):** added a generic goal.call
    example (`call goal BuildPage source=%a.b.c%` → Name="BuildPage"), measured 5/5 — **still
    `Name=%!app.module.file%`**. Root confirmed by the writer's own prompt: it receives a clean
    `=> formal: goal.call(Name)` slot (decider 0.96) and fills `Name` with the variable anyway. An
    **example reaches the decider, not the writer** (`properties.template` renders notes + type
    examples only; the action's Examples are in `decider.state.template`). So no teaching lever in my
    lane moves it — the note (writer-side) is already right, the example (decider-side) can't help.
    Example reverted. → **opaque-variable direction** (mask the step's variables in the *writer's*
    step text too, so `call goal Page module=%v1%` can't tempt the writer). Core, architect's.

The static code-path diagnoses below remain accurate about *mechanism*; the measured counts above
are the current-head truth. Original blocker note kept for the record.

---

## (original) Session blocker: no decider key → no real builds

Every `plang build` in this environment 403s at the decider:

```
HttpError (403): Must supply an API key!
  at /system/builder/BuildGoal/Decide.goal:20
    - llm.decider State=%state%, Question=%questions%, write to %answer%
```

- The decider is the **TypeSafe** decision service (`PLang/app/module/llm/code/TypeSafe.cs`),
  endpoint `https://api.typesafe.ai/v1/systemone`, key from `decider.apiKey` setting or
  `TYPESAFE_API_KEY` env.
- Neither is set: not in env (env has `OPENAI_API_KEY`, `DEEPSEEK_API_KEY`, but **not**
  `TYPESAFE_API_KEY`), not in `os/.db/system.sqlite` or `test/.db/system.sqlite`.
- TypeSafe is the **only** `IDecider` (grep: one impl). It's a bespoke typed-question service,
  not a general LLM, so OpenAI's present key can't substitute. There is no offline/mock decider
  for `plang build` (only `DeciderProviderTests.cs` mocks it for C# unit tests).

**Consequence:** I cannot produce measured counts (5/5, 3/3, …) for issues 25, 32, 2, or 30 this
session. Previous builder/educator sessions had the TypeSafe key provisioned. To measure, I need
`TYPESAFE_API_KEY` in env, or `set %!decider.apiKey% = "…"` stored in the build folder's settings.

What I **can** do without a live build: static code-path diagnosis of the pick/decider source
(more reliable than a trace for a which-branch question) and the template/shape work. Done below.

---

## Issue 25 reopened (C4 4/6) — diagnosis CONFIRMED (static), shape ready

**Symptom:** C4 (`lv-samples2/c4-1`, `c4-2`): `read 'receipt.txt', load vars, write to %receipt%`
builds `file.read(Path)` with **no `Template`**, even though the Option question answers `plang`.

**Confirmed by reading `PLang/app/goal/step/pick/list/this.cs`** (no trace needed — pure code path):

1. The chosen option value (`Template=plang`) is placed into the formal by **`Call(action)`**
   (lines 356-364): it appends `_option[{module}.{action}.{opt}]` values.
2. `Call` is invoked **only** from **`Prefill`** (line 312), which iterates
   **`_listed.Where(l => l.Mark == listed.Mark.Certain)`** (line 299).
3. An action is `Certain` only when `score ≥ Near (0.90)` and (for a through-module pick) its
   module share ≥ 0.90 (`Listing`, lines 283, 277-278).
4. In C4 `file.read` scored **under Near** (the architect's measurement), so it is marked
   `Possible`, not `Certain` → `Call` never runs for it → `Template=plang` is **never placed**.
5. The writer (`properties.template`) then sees `file.read` only on the `=> decider:` line
   (name + score + mark, **no option value** — template lines 39-43) and on `=> formal:` not at
   all. It writes `file.read(Path='receipt.txt')` from the step words and drops `Template`,
   because the Option answer `plang` never surfaced to it. (The lexical "load vars → Template"
   teaching is exactly the ~1/5 path issue 25's Option question was meant to replace.)

So the architect's hypothesis is exactly right: **Prefill fills a chosen option only for an action
that ends up Certain.** An action that is *listed but uncertain* carries no option value anywhere
the writer can see.

### Shape (core pick = coder; template = builder)

Make a listed-but-uncertain action carry its chosen option to the writer via the `=> decider:` line.

**Coder (core, `goal/step/pick/`):**
- `listed/this.cs`: add `public string? Option { get; init; }` — the action's chosen non-"none"
  options rendered as `Template=plang` (several joined with `, `), null when none.
- `list/this.cs` `Listing()`: populate `Option` per entry. Reuse the exact option-gathering that
  `Call` already does (lines 360-362: iterate `_option`, match prefix `{module}.{action}.`, skip
  `question.@this.None`). **Factor it into one reader** so `Call` and `Listing` share it — do not
  leave two copies (and keep it a member returning the assignments for an action name, not a
  `verb+noun` stray helper).

**Builder (mine, `properties.template` line 40):** render the option after the mark, e.g.
`{% if l.Option %} [{{ l.Option }}]{% endif %}` → `file.read 0.72 (possible) [Template=plang]`.
Prepared but not applied — it renders a field that doesn't exist yet; it lands with the coder's
`listed.Option`. (A notes/examples change isn't involved, so no Wire re-pin; but the
`properties.template` output changes, so the `prompt_c.py` twin + `PickListTests` must move in
lockstep — coder's twin, flag to architect.)

**Measurement owed (needs TypeSafe key):** C4 `c4-1`/`c4-2` → `Template=plang` on the first answer,
target 6/6; guard that a plain read (no load-vars) stays `Template`-free.

---

## Issue 32 — two shapes, diagnosis (static)

**(a)** `call goal Page module=%!app.module.condition%` adds a junk `condition.compare`.
**(b)** `call goal Page module=%!app.module.file%` builds `Name=%!app.module.file%` (the variable
takes the goal's name slot), 2 of 3.

**Root (shared), confirmed static:** `decider1.template` line 13 renders **`s.Text` raw** into
every question. The step text contains the full variable path, so the decider LLM reads the word
`condition` / `file` *inside* `%!app.module.condition%` as a word of the step and scores that module
up. There is **no masked form** of the step text (`grep Masked/Opaque/placeholder` on `step/this.cs`
= nothing).

**Shape (direction to measure — architect flagged it as a direction, not a committed fix):**
the decider should see each `%…%` as one opaque placeholder, so a name inside a variable is never a
word of the step.
- Coder (core): a `step` property that returns the text with each variable collapsed to a stable
  opaque token (e.g. `%v1%`, `%v2%`), driven by the same variable parser the pick uses
  (`type/item/variable/parser`).
- Builder (mine): `decider1.template` (and `decider2`/state where the step text appears) use the
  masked property instead of `s.Text`.
- **Caveat to weigh before building:** masking also hides a variable name that legitimately hints a
  module (rare). Measure on 32(a)/(b) *and* a control set where a variable name is the only signal,
  before committing. Needs the key.

**(b) extra:** check first against `2829786ff` — goal.call's note now says "the path is the whole
name … `/path/name`", which may make the writer read `%!app.module.file%` as a path-shaped name and
drop it into `Name`. If so, part of (b) is a goal.call note refinement (mine), independent of
masking. Needs a build to tell apart.

---

## Issue 2 with a key (1 of 6) — diagnosis (static), shape

**Symptom:** `foreach %person% as %value% with key %field%, call ShowEntry field=%field% value=%value%`
→ the `.pr` holds only `Collection`; both `Item` and `Key` lost; run fails "%field% not set".

**Static findings:**
- `pick/list/this.cs` `Code()` (the Known hint, lines 331-335) handles the collection (`First()`)
  and the item name via the **`As` regex** (`as %x%` → `Item`), but there is **no `with key %k%`**
  handling anywhere in `pick/` (grep confirmed). So Known/Prefill can bind `Item` but never `Key`.
- Item being lost too (the report's "both") is the writer dropping it — the same drop class as
  Conversation 26(1): a value the step names in a trigger word the writer can drop.

**Shape:** this is the **Option-question v2** lever (architect/Ingi, decision 496): a non-choice
option whose offers come from the step's own variables. `foreach`'s `Item`/`Key` would be offered
from the step's variables (`%value%`, `%field%`) + "none", Prefill writes them in. That's core
(pick + the v2 Option plumbing) and is blocked on Ingi's 496 ratification — the same gate as
Conversation. **Shape with the architect before building** (per the handoff). The `As`-but-no-`Key`
gap in `Code()` is a secondary coder fix for the Known-hint path.

---

## Issue 30 pass 2 — DONE (key available). All 15 remaining os/ goals build clean.

The sweep left **15** hand-authored os/ goals. Rebuilt per-file and as a full `os/` build, cache off.

**Why 0 rebuilt — a current regression (full diagnosis: `v3/cache-false-diagnosis.md`).** On head,
`cache:false` does NOT rebuild an unchanged goal **in any folder** (reproduced on the educator's
`hash-take`: build fresh, then rebuild unchanged with cache:false → "Found 1 goals", md5 unchanged).
Cause (corrected — see `v3/cache-false-diagnosis.md`): `Build.goal:7` `set default %!build.setting.cache%
= true` was a **redundant** copy of the class default (`build/setting/this.cs:10`), and it overrode a
CLI-provided `cache:false` so `Default.cs:116` merged the prior `.pr` → `goal.IsCached`
(`goal/this.cs:288`) → `BuildGoal/Start.goal` `if %goal.IsCached%, return` (skip). Deleting the line
fixed it; cache-off now rebuilds. (My first draft blamed a `%!build.setting.cache%`-reads-undefined /
`.setting`-projection bug — that was a **buggy debug watch** (`debug/this.cs:344,351` read only the
store for a reduced name); the coder confirms the setting reads correctly. No separate core bug.)
The LLM-cache half always worked (`Executor.cs:111`). build.md ll.69-71 (source-unchanged skip) are
correct. **At the time of pass 2 (before this fix landed)** a forced os/ rebuild needed a fresh
`.build` (off-limits); only stale-`.pr` goals rebuilt. With the fix in, cache-off rebuilds in place.

| goal | result | note | class |
|---|---|---|---|
| Start.goal | ✅ rebuilt (2 goals +sub HandleBuildFailure) | saved 21.7s | clean |
| system/builder/Build.goal | ✅ rebuilt | saved | clean |
| system/builder/BuildGoal.goal | ✅ current (skipped) | valid .pr | current |
| BuildGoal/Decide.goal | ✅ rebuilt (1 FixSteps retry) | `%!app.module.list% is in the step but not in your answer` → recovered, saved | clean (issue-17 class) |
| BuildGoal/Properties.goal | ✅ current | valid .pr | current |
| BuildGoal/Start.goal | ✅ current | valid .pr | current |
| BuilderChannel.goal | ✅ current | valid .pr | current |
| EmitBuildEvent.goal | ✅ current | valid .pr | current |
| error/Show.goal | ✅ current | valid .pr | current |
| shortcut/channel.goal | ✅ current | valid .pr | current |
| shortcut/error.goal | ✅ current | valid .pr | current |
| shortcut/goal.goal | ✅ rebuilt (1 FixSteps retry) | `%!app.callstack.scope.caller.goal% …not in your answer` → recovered, saved | clean (issue-17 class) |
| shortcut/step.goal | ✅ current | valid .pr | current |
| shortcut/test.goal | ✅ current | valid .pr | current |
| system/test.goal | ✅ current | valid .pr | current |

**Outcome:** no `writer-mis-map`, `core`, or `write-in-formal` refusal remains among the kept goals —
the v2-era failures (SetupApp, the events cluster, …) were all in the 24 goals the sweep deleted. The
5 goals that rebuilt this session (start, build, decide, buildgoal/start, shortcut/goal `.pr`) dropped
the deprecated `isSetup/isEvent/isSystem/isTest` fields (format update, 819f239c6; hash + steps
unchanged) — kept per "keep rebuilt .pr". The only noise is the **issue-17 class** (a `%!x%` the
answer carries differently reads as "in the step but not in your answer") — known, logged, and
FixSteps recovers it in one retry.

**Open for the architect:** to observe a forced rebuild of the 10 currently-skipped goals (to catch a
`.pr` that's current-but-stale-builder), the only lever is a fresh `.build` — off-limits for os/ under
the rule. Want a one-time authorized fresh rebuild, or is "current + the 5 clean rebuilds" enough?

## Item 5 — DONE
`os/system/Build_dpricated.goal2` deleted (`git rm`). v0.1 builder sketch, non-`.goal` extension,
only `.bot` bookkeeping referenced it.

## on.event slot type `app.event` → `event` — two test .pr rebuilt (architect task)

Two built test `.pr` under `test/` still typed `on.event`'s `Event` slot as `app.event`; the type is
`event` since `43441c5c9` ("the event type declares its word: event", 11:53 today).

**Stale-binary catch:** my binary was built near session start, *before* `43441c5c9` entered my tree
via a later rebase — so the first rebuild still emitted `app.event` AND showed a phantom `goal.call
Name = <whole step text>` mis-map. Rebuilt the binary clean (`dotnet build PlangConsole`) → both
anomalies gone. (My earlier cache fix is unaffected — Build.goal doesn't touch the event type.)

Rebuilt each alone, cache off, from `cwd=test/` (app root `test/.build/app.pr`).

### CreateFiresOnBirth (file one) — DONE, `event`, test green, COMMITTED
`app.event`→`event`; **test Pass**. Build flaky on the issue-17 class (step 4's assert message literal
refused ~2/3, FixSteps recovers ~1/3; saved on attempt 2). Diff besides the type: `asdefault`→
`default`; `goal.call Name` type `text`→`goal` (value unchanged); dropped default-false options
`resolvevariables=false` (file.write), `ignoreifnotfound=false` (file.delete); format drops
(`waitForExecution`, `isSetup/isEvent/isSystem/isTest`); one benign warning (math.add unsure 0.98).

### AsPathIsABirth (path one) — HELD at committed (`app.event`, green); two blockers, both not my lane
Rebuilding it to `event` does NOT stay green. Two separate problems:
1. **`as path` coercion dropped ~3/5** — the writer drops `Type=path` from `set %p% = "a.txt" as path`
   (measured: Type=path present only 2/5; `app.event`→`event` 5/5). Without `Type=path` the conversion
   isn't a birth, `%created%` stays 0, assert fails. This is the droppable-trigger-word class (the
   Option-question lever): `Type` is like a chosen option the writer loses. `set.notes.md:3` teaches
   `as <type>` generically and examples cover `as text/int/date/duration/image` but **not `as path`**;
   notes reach the writer, examples only the decider, so neither reliably lands `path`.
2. **Even with `Type=path` present, the runtime fails** `'app.event.on.create' has no wire contract —
   declares no [Out]/[Store]`. So renaming the slot to `event` (per 43441c5c9) surfaces a **core gap**:
   the path create event has no wire-serializable face, while `file.on.create` does (file test passes).
   The old `app.event`-typed `.pr` passed because that path didn't hit the contract check.

So the path `.pr` is **kept at its committed state** (`app.event`, Type=path, **test green**). Its
event-type update waits on the core wire-contract fix (and the `as path` mapping is a separate builder
reliability gap). Both handed to the architect/coder. **Verified both tests green in the end:
AsPathIsABirth Pass (committed .pr), CreateFiresOnBirth Pass (rebuilt .pr).**

Stale-binary note kept (above): the first rebuild emitted `app.event` + a phantom goal.call-name
mis-map because the binary predated 43441c5c9; a clean `dotnet build` fixed both.

## Pick-pass integration (coder 814ca5209) — builder half + measurements

**Landed (d08a129df):** the four LLM-facing templates read `s.Mask.Text`; `properties.template`'s
`=> decider:` line renders `listed.Option` (`[Template=plang]`); `ask:` lines on loop.foreach
Item/Key and llm.query Conversation (reworded off the old `{continue: %x%}`); `pick_golden.json`
re-pinned (masked step text + the new Option questions); Wire 487 pass / 9 fail (baseline) / 8 skip.

**Measurements — fresh, cache off, 5 each:**

| # | step | result (5 builds) | verdict |
|---|------|-------------------|---------|
| control | `save %!llm.setting.cache%` | file.save mis-map → refused, **0/5 build** | ⚠️ **masking REGRESSES it** |
| 2 | `foreach %person% as %value% with key %field%` | Item+Key **5/5** | ✅ fixed (was 3/5) |
| 32(b) | `call goal Page module=%!app.module.file%` | Name=`%!app.module.file%` **5/5** | ❌ not fixed |
| continue | `continue the conversation %answer%` | Conversation **5/5** | ✅ fixed |
| 33 | `set %p% = "a.txt" as path` | Type absent **5/5** | ❌ Option doesn't reach Type |
| guard | plain `foreach %items% as %i%` | Item, no Key **5/5** | ✅ |
| guard | plain `read 'notes.txt'` | no Template **5/5** | ✅ |
| guard | bare `continue the conversation` | none 3/5, Conversation **2/5** | ⚠️ flaky |

**Readout:**
- **Issue 2 and the named continue are fixed** by Option-v2 (offers = the step's placeholders);
  the plain-foreach and plain-read guards hold.
- **Control regression (the architect's worry, confirmed):** `save %!llm.setting.cache%` masks to
  `save %v1%`, which strips the only module signal (`setting`, inside the variable path) → the decider
  picks `file`, the writer writes `file.save(Path=%!llm.setting.cache%)`, and it refuses (a bool can't
  be a Path). So masking costs a step whose module is decided by a `%!…setting…%` variable's name.
  Decision for the architect: accept (rare), or exempt `%!…%` paths from masking, or keep that signal.
- **32(b) NOT fixed by masking.** The writer receives the correctly-masked
  `[0] - call goal Page module=%v1% => formal: goal.call(Name)` and still answers `Name=%v1%`
  (restored to `%!app.module.file%`), dropping `Page` — 5/5. Masking fixed **32(a)** (the decider no
  longer reads `file`/`condition` inside the variable), but 32(b) is a **writer** bug: it prefers the
  variable over the bare word `Page` as the goal name. Needs goal.call teaching or more — still open.
- **33 (`as path`) not covered.** The Option question does **not** reach `set.Type` — `set.notes.md`'s
  Type line has no `ask:`, and `Type`'s offers would be **type names** (a choice), not the step's
  placeholders, so it needs a different Values source, not just an `ask:` tag. `Type=path` drops 5/5.
- **Bare-continue guard flaky:** 2/5 the decider picks `%answer%` (in scope from the prior step)
  instead of `none`, adding Conversation where Ingi ruled a bare continue is null.

## 32(b) note-lever (architect-requested, while idle) — PARTIAL (0/6 → 4/5)

32(b) had turned consistent (6/6 `goal.call(Name=%!app.module.file%)`, `Page` dropped) — worsened by
my `2829786ff` note ("the path is the whole name"). The note reaches the **writer** (unlike examples),
so I refined `call.notes.md`: the Name is the **one token right after `call`/`call goal`** (bare word
or slash path), and a **`name=value` after it is always a Parameter, never the name — even when the
value is a dotted variable** (`call goal Render source=%!a.b.c%` → `Name="Render"`, not `%!a.b.c%`).
Added one example in that shape. Re-pinned `pick_golden.json` (word-diff: only the goal.call teaching
text moved).

**CORRECTED (my first 4/5 did not reproduce).** The educator got 0/8; a clean rebuild + 3-way
re-measure (fresh, cache off, 5 each, educator's command `plang build '--app={"create":true}'
'--build={"cache":false}'`, direct step-0 Name inspection):

| set | goal | step-0 Name ×5 | Page |
|---|---|---|---|
| A edu (educator's exact, `module=`, `%module%` used in Page) | — | `%!app.module.file%` ×5 | **0/5** (reproduces 0/8) |
| B guards (my earlier b32 shape, `module=` + 2 guard steps) | — | Page, Page, bug, bug, bug | **2/5** |
| C mparam (`m=` instead of `module=`, `%m%` used) | — | Page, Page, bug, Page, Page | **4/5** |

So: **the note does NOT fix 32(b) for `module=`** (my earlier 4/5 was a flaky/extraction artifact on
the guards goal — retracted; the honest number for `module=` is 0–2/5, 0/5 on the educator's exact
goal). **The parameter name is the aggravator** (educator's guess confirmed): `module` is a plang
concept and echoes the variable path `%!app.module.file%`, so the writer reads the value as the name;
rename it `m=` and it jumps to 4/5. The guard steps (more goal.call context) nudge it 0→2/5 but not
reliably. **The note is kept** (harmless, helps the non-`module` cases and the decider), but the real
fix is the coder's **goal-name offers** (the `goal` type's `Offers(step)`, queued) — the decider picks
`Page` from the reachable goals, immune to the parameter's name. Re-measure after that lands.

## `%!…%` teaching (masking removed; coder 8a4ef5fc7) — builder half + measurements

Masking was ruled a hack (Ingi) and removed by the coder (templates back to `s.Text`, pick_golden
re-pinned to real variables). My half: added the `%!…%` teaching (both sentences, architect-approved)
to `decider.state.template` (shared state) and `Properties.llm` (value legend + the `call X name=value`
line); re-pinned pick_golden (word-diff: only the teaching text, 18 cases). My `l.Option` render and the
`ask:` notes carried over unchanged.

**Measured (fresh, cache off, 5 each; batch-extractor counts CORRECTED by direct `.pr` inspection —
the grep over-counted `Key`/`Page`, same lesson as the retracted 4/5):**

| scenario | verified result | verdict |
|---|---|---|
| A edu `call goal Page module=%!app.module.<m>%` | Names still the variable; **condition.compare 0/5** | **32(a) FIXED** (teaching); 32(b) unchanged |
| C mparam (`m=`) | mostly refused/variable post-masking | moot — 32(b) waits on offers |
| control `save %!llm.setting.cache%` | **→ setting.save 1.00** (was file.save under masking) | **regression RESOLVED**; residual = issue-17 class |
| issue 2 `foreach … as … with key …` | Collection+Item+Key present | **FIXED** |
| continue `continue the conversation %answer%` | Conversation present (4/5 built; 1 flaky NOPR) | **FIXED** |
| guard plain `foreach %items% as %i%` | Item, no Key | holds |
| guard plain `read 'notes.txt'` | no Template | holds |
| guard bare `continue the conversation` | no Conversation | holds |

**Readout:**
- **The `%!…%` teaching fixes 32(a)** — the decider no longer reads `condition`/`file` inside
  `%!app.module.<m>%` as a step word (condition.compare junk gone, 0/5), and **fixes the control
  regression** — `save %!llm.setting.cache%` maps to `setting.save` (the `setting` signal is honored),
  not `file.save`. (The control's residual refusal is the separate **issue-17** class: Cover refuses a
  `%!…%` the answer carries in another form.)
- **32(b) is unchanged by the teaching** — the writer still picks the variable as the goal `Name` over
  the bare word (`Name=%!app.module.file%`, not `Page`). As established, the fix is the coder's
  **goal-name offers** (the `goal` type's `Offers(step)`), which the decider picks `Page` from — the
  `%!…%` teaching is the decider-signal half, the offers are the writer half.
- **issue 2, the named continue, and all three guards hold.** Masking is gone with no loss on the wins.

Discipline note: the batch grep mis-reported `Key` (false match) and `Page` (anchor) — every row above
was re-checked by direct `.pr` inspection before reporting. (Same failure mode as the retracted 4/5.)

## Goal-name / type offers (coder d2aa1358d) — `ask:` lines + re-measure: 32(b) & 33 FIXED

Added the `ask:` lines on `goal.call` Name ("which goal does the step call?" → offers the reachable
goals) and `variable.set` Type ("which type does the step coerce the value to?" → offers the plang
type names). Re-pinned pick_golden (word-diff: only the two new Option questions). Measured fresh,
cache off, 5 each, direct `.pr` inspection:

| scenario | result | verdict |
|---|---|---|
| 32(b) `call goal Page module=%!app.module.<m>%` (educator's exact goal) | Name="Page" ×3 on **4/5**; 1 NOPR (flaky build fail, retry built clean — no variable-as-name bug) | **FIXED** (was 0/5) |
| 33 `set %p% = "a.txt" as path` | `Type="path"` **5/5** | **FIXED** (was 0/5) |

So the decider now picks `Page` from the reachable-goal offers (immune to the parameter's name) and
`path` from the type-name offers — the writer half 32(b)/33 needed. With the `%!…%` teaching (32a) and
these offers (32b/33), the whole goal.call-name / coercion family is closed.

## Issue 34 (os bot) — `list<permission>` element-form trace (measurement blocked: terminal not on app-systems)

**Trace (file:line, what it prints):** a slot's type reaches the writer at
`os/system/builder/llm/templates/properties.template:86` — the Types section prints
`- <type> — <Description> — one of: <Values> (e.g. <Example>)`, where `<type>` is `{{ p.Type }}` and
the fields are `p.Type.{Description,Values,Example}`. For a `list<permission>` slot it prints the
**list type's own face** (and the kind name `permission`) and **never recurses into the `permission`
element** — so the `{path, verbs}` Example/Shape is not shown. This matches the underlying type face
`PLang/app/type/this.cs:76`: a kinded type writes only `writer.String(kind.Name)`, not the element's
Example/Shape. **Confirmed the architect's hypothesis.**

**Fix (architect-refined) — the TYPE answers its own Example, NOT the template.** The type is the one
door: `properties.template:86` reads `p.Type.{Description,Values,Example}`, and so does the
`%!app.type.x%` Out face (`type/this.cs:62-83`). If the template recursed into the kind, that second
reader would need the same recursion — **stored twice**. So `list<permission>` answers its own
`Example` built from its element's (`[{"path": …, "verbs": […]}]`) and its `Description` names the
element; the template stays unchanged. **Core → the coder.**
**My part (when it lands):** measure a `list<T>` slot on app-systems whose element has an `Example`
(find one, or name the gap), guard `list<text>` prints as today. **34's 5-build and 35's 10-build
terminal measurements are BLOCKED** until the terminal module reaches app-systems (`start //bin/sh …`
and the `list<permission>` slot aren't buildable here).

## Issue 35 (os bot) — `//` drop rate: measurement blocked (terminal not on app-systems)
Already diagnosed as the writer dropping `//` (the path type preserves it). The os bot confirms the
raw `.pr` held `"/bin/sh"` in the failing runs and `//bin/sh` in 4 on b517c9e61 → intermittent,
writer-side. The 10-build drop-rate measurement needs the terminal module, not on app-systems —
blocked here; runs on plang-os-stable or once terminal merges.

## `formal.Writer` follow-up (coder 4af605b25) — regression check: nothing dropped

The coder's follow-up (offers are items via `formal.Writer`; Find and Offers share one walk) changes one
byte the writer sees: a chosen choice option rides the starting line **quoted** (`Template="plang"`, was
`Template=plang`). Re-measured the fixed issues (fresh, cache off, 5 each; hash re-checked with a wider
grep after the `-A3` window missed the value — no code change, extractor fix only):

| issue | step | result | verdict |
|---|---|---|---|
| 25 | `read 'receipt.txt', load vars` | Template present **5/5** | holds |
| 28 | `hash "…" with sha256` | `Algorithm="sha256"` **5/5** | holds |
| 2 | `foreach … as %value% with key %field%` | Item+Key **5/5** | holds |
| 32(b) | educator's `module=%!app.module.<m>%` | Name="Page" on **3/5 built**, 2 flaky NOPR (no mis-map) | holds |
| 33 | `set %p% = "a.txt" as path` | `Type="path"` **5/5** | holds |

The quoting change is safe — every fixed issue holds. (32(b)'s NOPR is a flaky build-completion issue
on the educator's nested goal, not a mapping regression; when it builds, the Name is `Page`.)

## Three investigations (architect, 2026-10-02)

### 1. The 32(b) "NOPR" has a cause — a CORE cast bug, not flaky (NEW ISSUE)
edu built 6/10 this time; the 4 failures were **all the same**, during `Properties rejected —
retrying`:
> `InvalidCastException: cannot lower a app.module(plang module) into list: the target owns no Clr
> projection for this shape. A cross-shape convert (list→list, dict→record, raw→plang) belongs on the
> TARGET's own Clr or its type family, not this lower door.`
So building `call goal Page module=%!app.module.<m>%` sometimes lowers the `%!app.module.<m>%` value (a
plang **module**) into a **list** and the cross-shape convert throws — the retry dies there, no `.pr`.
**Core** (the lower door / the target's Clr family), **the coder's** — a new issue, not a 32(b)
mapping regression (when it builds, `Name="Page"`). Rate ~4/10 on the educator's nested goal.

### 2. Issue 36 reproduces — `%item%` in foreach's Key slot
`foreach %x%, call Y y=%item%` builds `loop.foreach(Collection=%x%, **Key=%item%**)` with **no Item**
(~2/5). The auto-bound iteration var `%item%` is wrongly placed in Key. Same lever as issue-2-with-key:
the Key Option offers the step's variables and the decider fills Key with `%item%`. The fix: the Key
Option must not be filled from `%item%` (foreach's own iteration var) — Key defaults to none, Item to
`%item%`, unless the step says `with key`. **Guard holds:** `foreach %items% as %i%` → Item=%i%, no Key
(5/5, direct inspection). Core (the pick/Option) + teaching; shape with the architect.

### 3. "hash the file 'x.bin' with sha256" — file vs text (measure-only, Ingi's question)
Built (fresh, cache off, 5): **reads the file first 4/5** — `file.read('x.bin') → %!data%`, then
`crypto.hash(Content=%!data%, Algorithm=sha256)`; **hashes the literal text `"x.bin"` 1/5**
(`crypto.hash(Content="x.bin")`, no read). So the builder mostly treats "the file 'x.bin'" as a file to
read-then-hash, but 1/5 hashes the name's letters. No path-coercion (`as path`) appears — it's a
`file.read` before the hash. **Reported, not fixed:** whether a text naming a file should always hash
the file's bytes is Ingi's call, not a builder teaching change.

## Item 6 — gated on the coder's stages 1–2 of `test/plan/task/` (not started).
