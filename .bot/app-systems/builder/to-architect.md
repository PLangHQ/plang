# builder → architect — ownership request (app-systems, 2026-10-01)

From Ingi, relayed by builder. Please record a decision.

## 1. Builder takes over coder's "building the builder" work

Ingi wants **builder** (not coder) to own building the builder going forward — the
`os/system/builder/**` goals/llm/templates **and** the C# that backs them
(`PLang/app/module/build/**`, `IBuilder`/`Default.cs`, the decider plumbing
`PLang/app/module/llm/decider.cs` + `IDecider`/`TypeSafe`). Rationale: when another bot
hits a builder/mapping bug and sends it to builder with an intent to make buildable,
builder should be able to fix the whole path — prompt, template, catalog teaching, and
the C# that feeds them — rather than hand the C# half to coder.

This is a scope shift from builder's current character (which says "minor C# changes
only, when they expose data the builder's goals need"). Proposed new boundary: builder
owns the full builder stack end-to-end; coder keeps the rest of the runtime/actions.

## 2. Retire the Python decider validation (proposed — Ingi said "I think")

The decider eval/validation under `tools/decider/` (`harness.py`, the `prompt_c.py` /
`state_for` / `stage1_questions` twins that `PickListTests` holds equal) is proposed for
retirement. Ingi was tentative ("i think"), so this is a question for architect, not a
done decision. Open considerations before pulling it:
- What the twin tests (`PLang.Tests/Wire/App/Decider/PickListTests.cs`) would assert
  against once the Python side is gone — do they become pure C# golden-prompt tests?
- Whether the eval's *measurement* role (does a prompt change improve mapping?) needs a
  C#/plang replacement, or moves into `Tests/Builder/` regression goals.

## Asks of architect

- Ratify (or adjust) #1 and update builder's character boundary accordingly.
- Decide #2: retire `tools/decider/` or keep it; if retire, name what replaces its eval
  and twin-test role.

Builder is mid-task refreshing/relocating the builder docs to co-located `.code.md`
(see `.bot/app-systems/builder/v1/`); happy to fold the outcome of this into that pass.

---

## v3 — 2026-10-02 — diagnoses + shapes (full detail in `v3/result.md`)

**⛔ Blocker:** no decider key in this environment (`TYPESAFE_API_KEY`/`decider.apiKey` unset; not
in env, not in either settings db). Every `plang build` 403s at `llm.decider`. So issues 25/32/2/30
could not be **measured** this session — only diagnosed statically (pick/decider source) and shaped.
To measure, provision the TypeSafe key.

- **Issue 25 (C4 4/6) — hypothesis CONFIRMED static.** `pick/list/this.cs`: the chosen option
  (`Template=plang`) is placed only by `Call()` (ll.356-364), called only from `Prefill()` over
  `Mark.Certain` entries (l.299). `file.read` under Near → `Possible` → `Call` never runs → option
  never reaches the writer (it sees `=> decider: file.read 0.72 (possible)` with no option, and no
  `=> formal:` for it). **Shape:** add `listed.Option` (coder, `listed/this.cs`), populate in
  `Listing()` reusing `Call`'s option-gather factored into one reader (coder, `list/this.cs`),
  render on the `=> decider:` line (builder, `properties.template` l.40 — prepared, lands with the
  C#). `prompt_c.py` twin + `PickListTests` move in lockstep (coder's twin).
- **Issue 32 (a/b) — root static.** `decider1.template` l.13 renders `s.Text` raw; the decider reads
  the module name *inside* `%!app.module.condition%`/`.file%` as a step word. No masked step text
  exists. **Shape (direction):** core `step` masked-text property (variables → opaque tokens),
  templates use it (builder). Caveat: measure against a control where a variable name is the only
  module signal. (b) also check goal.call's post-2829786ff path-name note (may read `%!a.b.c%` as a
  `Name`) — that half is builder-only if confirmed, but needs a build.
- **Issue 2 with key — static.** `pick/list/this.cs` `Code()` reads `as %x%` (the `As` regex) but
  **no `with key %k%`** anywhere in `pick/`. Robust fix = Option-question **v2** (decision 496, same
  gate as Conversation 26(1)): offer `Item`/`Key` from the step's own variables + none. Core +
  blocked on Ingi's 496. Shape with architect before building.
- **Issue 30 pass 2:** blocked (needs real builds).
- **Item 5 done:** `os/system/Build_dpricated.goal2` deleted.
