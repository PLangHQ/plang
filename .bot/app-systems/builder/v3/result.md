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

## Issue 30 pass 2 — BLOCKED (needs real builds)

Rebuilding each reopened `os/` goal individually, cache off, requires the decider → 403. Cannot
produce the table this session. Deferred until the key is available.

## Item 5 — DONE
`os/system/Build_dpricated.goal2` deleted (`git rm`). v0.1 builder sketch, non-`.goal` extension,
only `.bot` bookkeeping referenced it.

## Item 6 — gated on the coder's stages 1–2 of `test/plan/task/` (not started).
