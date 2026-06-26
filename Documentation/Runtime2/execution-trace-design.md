# Execution Trace — Design for Architect Review

**Status:** proposal / prototype landed under `tools/trace-viewer/`
**Branch:** `execution-trace` (from `compare-redesign`)
**Author handoff:** Ingi + Claude → architect

> **Settled model (2026-06-26):** the graph is built from **call edges** (`A → B` = A called B), and a **fork** is a node whose callees differ across runs, detected by distinct-run-counts — not the sequence edges / out-degree framing in §2, §3.1, and §5 below. v1 surfaces forks only. The buildable model, capture format, and a worked example with data live in `.bot/execution-trace/architect/fork-graph-model.md` + `example.ndjson`. The "why" (§1), the `trace` concept, Fody-vs-Harmony (§4.3), and the open decisions (§7) still stand.

---

## 1. Why

There is no high-level overview of *what execution path the runtime actually takes*. The
motivating incident: reading a variable takes a **different parsing path when the actor
context is null** (typed reader → verify → `type.Build` vs `json.Parse` → unverified →
`type.Judge`). Ingi was blind to that fork — nothing surfaced it.

Because of OBP, every path is pointer → pointer → pointer until it funnels into the same
shared implementation leaf. So:

> **The deeper we go in the call tree, the more the same it should be.**

That inverts what's worth showing. The shared leaves are the boring trunk. **A fork is the
event** — and a fork whose arms *never reconverge* (they end at different results) is the
alarm. The goal is a single overlaid flow graph where the eye goes straight to the forks
(the canvas in §3 — that is the target render).

What the user wants to answer at a glance: *"when X happens, this is the path"*, and *"are
we going down the wrong path?"* — at PLang altitude (goal → step → action) **and** C#
altitude (the `Variable.get → … → leaf` method chain).

---

## 2. The one truth that shapes everything

**A single execution only ever takes one arm of a fork.** When context is present, the
`no context` calls never run, so a single recording cannot contain the other arm's subtree.

Therefore the merged tree is **not captured directly — it is derived by overlaying runs:**

```
run 1 (ctx)      run 2 (no ctx)         canvas flow graph
   path      +       path        ──►   one bubble per method (shared)
                                       successors differ  → fork (purple)
                                       predecessors merge → reconverge (green)
                                       lanes never rejoin → distinct outcomes
```

Consequence for the architecture: **the runtime's only job is to honestly record one path
per run** (flat, cheap, append-only). Fork/merge detection lives in the **viewer's overlay**
over 2+ recordings (§3.1). The hot path stays dumb.

---

## 3. Prototype already in the repo

`tools/trace-viewer/`. **The primary target is the canvas flow graph** (`canvas.html` +
`flow.ndjson`) — that is the view to build toward. The tree view is kept as a secondary
reference.

**Primary — canvas flow graph:**

- `canvas.html` — zero-dependency canvas viewer. Each **method is a bubble** (deduped by
  `sig`), edges are the **next call in execution** colored per run. Horizontal layered
  layout: **entry far left, outcomes far right**, execution flows left→right; a bubble's
  column = its longest path from entry, so a merge always sits right of its predecessors.
  Forks ringed purple, merges green, draggable bubbles, hover tooltip.
- `flow.ndjson` — the per-run capture format the canvas consumes (see §5).

**Secondary — merged tree (kept, not the focus):**

- `index.html` + `tree.json` — the merged-tree render (one trunk, inline `⑂` forks, red when
  arms never reconverge). Useful as an alternate lens; not the build target.
- `demo.ndjson` — earlier raw two-run NDJSON, superseded by `flow.ndjson` as the capture
  reference.

Serve: `cd tools/trace-viewer && python3 -m http.server 8777` → open `/canvas.html`.

### 3.1 The bubble-graph model (what the canvas encodes)

A run is a **path** through methods — the ordered sequence of method entries. Overlay N runs
on a shared set of bubbles (one bubble per `sig`). Edges = consecutive pairs in each path,
tagged with which run(s) traversed them. Then:

- **Fork** = a bubble whose set of *successor* bubbles differs across runs (out-degree
  divergence). "Same bubble, different next call." This is the alarm.
- **Merge / reconvergence** = a bubble reached from ≥2 distinct *predecessor* bubbles
  (in-degree convergence). "Different paths, same bubble again." Expected — it's the trunk
  re-forming. A bubble can be **both** (rejoin then split again).
- **Outcome** = terminal bubble per run (the return value). Lanes that never reconverge end
  at different outcomes — the divergence that matters.
- Shared edges (all runs) draw neutral; run-specific edges draw in that run's color. The eye
  reads the colored lanes peeling off and rejoining the gray trunk.

This is purely structural: fork/merge fall out of the overlay with **no branch annotation**
required. The runtime just emits each run's ordered entries; the canvas builds the graph.

---

## 4. Architecture

### 4.1 `trace` concept + sink, armed by scope

Add a `trace` concept following the `app.X` collection convention (folder `app/trace/`,
`app/trace/this.cs` = `trace.@this`). It has a **current** (execution flows through it):
`app.trace.current` = `AsyncLocal<Sink?>`.

- **Off by default.** `Current == null` → every hook is a single null check. ~zero cost.
- **Armed by scope**, mirroring `--debug`: `--trace={"goal":"Start"}` sets the sink on entry
  to that goal's frame, flushes + clears on exit. Reuse the pattern in
  `app/module/debug/this.cs` `Apply` (which already reads `--debug` and mutates
  `CallStack.Flags`). You never trace the whole app — only the scoped subtree.
- **Sink** appends NDJSON to `.bot/<branch>/trace/<runid>.ndjson` **through the path verbs**
  (`app.type.path` `WriteText`/append) — `System.IO` is banned (PLNG002). One run = one file.

### 4.2 PLang-altitude nodes (cheap; all data already exists)

The call tree is already captured — `app.callstack.call.@this` has `Caller`/`Children`,
`StartedAt`/`Duration`, `Diffs`, `Tags`, `Errors`, `Id`, `Synthetic`. Its own docstring says
*"Render-agnostic: same data folds into a stack, flamegraph, or timeline."* We just emit it.

Hooks:

| Event | Site | Data available |
|---|---|---|
| `Enter` | `CallStack.Push` — `app/callstack/this.cs:104` | `call.Id`, `caller?.Id`, kind from `Action.Step.Goal`, sig from `Goal.PrPath` |
| `ret` | `call.ExecuteAsync` — `app/callstack/call/this.cs:233` | `result` (the `Data`) is in hand here |
| `Exit` | `call.DisposeAsync` — `app/callstack/call/this.cs:269` | `Errors`, `Diffs`, `Duration` |

`kind` = goal / step / act, derived from the action's position. This slice alone yields the
goal→step→action trunk of a real run — **no weaving required.**

### 4.3 C#-altitude nodes (the method-chain depth)

Source generators can't rewrite existing method bodies, so this needs IL weaving.

- **Fody `MethodBoundaryAspect`**, allowlisted to `app.*` / `PLang.*`.
  - `OnEntry`: `if (Trace.Current == null) return; sink.Enter(newId, csParent ?? CallStack.Current.Id, "cs", method, file:line)`
  - `OnExit`: `sink.Exit(id, returnValue)`
- A **second `AsyncLocal`** tracks the current C# frame so C# nesting parents correctly;
  with no C# parent, the node hangs under the current PLang frame. This is what unifies both
  altitudes into **one** tree.
- Skip property getters/setters by default; allowlist keeps BCL out.

**Alternative:** Harmony (runtime patching) — zero baseline cost, armed on demand, but more
fragile. Recommend **starting with Fody**; revisit if the always-compiled-in null check
matters.

### 4.4 Fork enrichment (optional, additive)

Structural forks fall out of the merge for free (any node whose children differ between
runs). The merge knows *that* it forked, not *why*. To label the condition, drop a one-liner
at the branch site:

```csharp
Trace.Fork("_context == null", at: "Wire.cs:211");
```

No annotation → still a red fork, labeled "diverges" instead of with the condition. Start
unannotated; annotate the handful of branches that matter.

### 4.5 Overlay — in the viewer (JS), nothing extra in the runtime

The canvas builds the graph itself (§3.1): read each run's ordered entries → one bubble per
`sig` → edges between consecutive entries tagged by run → fork = successor set differs by run,
merge = ≥2 distinct predecessors. **No merge tool, no runtime comparison code** — the runtime
only emits flat per-run `flow.ndjson`; the canvas does the rest. This is implemented today in
`canvas.html` and is the reference behaviour.

(The secondary tree view derives a nested `tree.json` by aligning runs and applying a
reconvergence rule — kept for reference, not the build target.)

---

## 5. File formats

### `flow.ndjson` — the format the runtime emits (primary)

One line per event, in execution order, per run. This is the canvas's input.

```
{"e":"meta","title":"variable.get %user% — execution flow graph"}
{"e":"run","run":"A","label":"with context","color":"#7aa2f7"}
{"e":"run","run":"B","label":"no context","color":"#e0af68"}
{"e":"step","run":"A","sig":"data/Wire.cs:316","n":"Wire.ReadBody"}
{"e":"step","run":"A","sig":"type/reader","n":"Type.Readers.Typed"}
...
{"e":"end","run":"A","ret":"User{ id:7, name:\"Ingi\" }  ✓ typed, verified"}
```

- `run` declares a run (id, display label, lane color).
- `step` is one method entry: `sig` (bubble identity) + `n` (display name). Emit in execution
  order; the canvas pairs consecutive steps into edges.
- `end` is the run's terminal outcome (`ret`).

Crucially: **a real run only emits its own path** (one arm of each fork). Run the action
under different conditions (with/without context) → two runs in the file → the canvas
overlays them and the forks/merges appear. The runtime never has to know about the other arm.

### `tree.json` — secondary, derived (reference only)

Nested merged-tree shape consumed by `index.html`. See `tools/trace-viewer/tree.json`.

---

## 6. Build order

1. **Slice 1 — emit `flow.ndjson`.** `trace` concept + sink, `--trace` scoping, hook
   `CallStack.Push` to emit a `step` line (sig from `Goal.PrPath` / action) and `call`
   exit to emit `end`/`ret`. PLang-altitude only, no Fody. Run an action twice (with/without
   context) → drop both files in `canvas.html` → forks appear. (~½ day, reuses existing call
   data.) **Smallest end-to-end proof against the canvas.**
2. **Slice 2 — Fody weaving.** Add C# method `step` lines (one bubble per C# method) under
   the PLang frames — this is the depth that makes `variable.get → … → leaf` visible.
3. **Slice 3 — run management.** A convenient way to capture the same action under N
   conditions into one `flow.ndjson` (or N files the canvas loads together).
4. **Slice 4 — `Trace.Fork` enrichment** to label *why* a bubble forks (the condition text),
   for the branches that matter.

---

## 7. Decisions for the architect

1. **`trace` as a first-class `app.X` concept** (folder `app/trace/`, `app.trace.current`)
   vs. folding the sink into the existing `CallStack.Flags` tier (alongside Timing/Diff/
   History). The concept is cleaner OBP; the flag is less surface. Recommendation: concept.
2. **Fody vs Harmony** for C# weaving (§4.3). Recommendation: Fody first.
3. **Bubble identity (`sig`)** — this is the load-bearing choice for the canvas, since bubbles
   are deduped by it and forks/merges are defined by successor/predecessor sets over it.
   `file:line` shifts as code moves; `Namespace.Method` is stable but collapses overloads and
   loop iterations into one bubble (often desirable). Recommendation: `Namespace.Method`,
   revisit if too coarse.
4. **Capturing multiple runs** — how does the user request "run this action with and without
   context"? Re-run the app under different inputs, or a single harness that drives both? The
   canvas needs ≥2 runs in the file to show any fork.
5. **Size / loops** — a method in a `foreach` becomes one bubble with a self-loop or repeated
   edges; that's naturally compact (the graph dedups). Decide whether to show edge
   traversal counts / weights, and any cap on distinct bubbles per capture.

---

## 8. OBP / convention fit

- `trace` is a no-`.current`-violating concept: execution *does* flow through it, so
  `app.trace.current` (the active sink) is correct per the `app.X` rule.
- Sink writes via path verbs only — no `System.IO` (PLNG002 at error severity).
- No `Console.*` — diagnostics already route through channels; trace is its own sink file.
- The capture reuses `call.@this`'s existing tiers; it adds a **renderer/sink**, not a
  parallel data store — consistent with "render-agnostic" already on the type.

---

## 9. Cost

- **Off:** one `AsyncLocal` read per `Push` (already paid) + one null check per woven `app.*`
  method. Negligible but non-zero with Fody (compiled into every method).
- **On, scoped:** bounded to the `--trace` subtree; append-only NDJSON; flat memory (nothing
  retained in RAM — the file is the record, the viewer builds the graph from it).
