# Execution Trace — Design for Architect Review

**Status:** proposal / prototype landed under `tools/trace-viewer/`
**Branch:** `execution-trace` (from `compare-redesign`)
**Author handoff:** Ingi + Claude → architect

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
alarm. The goal is a single merged call tree where the eye goes straight to the forks.

What the user wants to answer at a glance: *"when X happens, this is the path"*, and *"are
we going down the wrong path?"* — at PLang altitude (goal → step → action) **and** C#
altitude (the `Variable.get → … → leaf` method chain).

---

## 2. The one truth that shapes everything

**A single execution only ever takes one arm of a fork.** When context is present, the
`no context` calls never run, so a single recording cannot contain the other arm's subtree.

Therefore the merged tree is **not captured directly — it is derived by overlaying runs:**

```
run 1 (ctx)      run 2 (no ctx)            merged tree.json
   tree      +       tree         ──►   shared nodes drawn once
                                        children differ   → fork
                                        arms rejoin       → reconverges (green)
                                        arms don't rejoin → red
```

Consequence for the architecture: **the runtime's only job is to honestly record one path
per run** (flat, cheap, append-only). Fork detection and reconvergence live in a separate
**merge step** over 2+ recordings. The hot path stays dumb.

---

## 3. Prototype already in the repo

`tools/trace-viewer/`:

- `tree.json` — hand-authored **merged** tree for `variable.get %user%`, modelling the three
  real context-null forks (`Wire.cs:374`, `Wire.cs:211`, `this.cs:299`).
- `index.html` — zero-dependency viewer. One trunk; `⑂ fork` markers inline; red tag when a
  fork's arms never reconverge; "dim C# leaves" toggle; "final outcome per path" footer.
- `demo.ndjson` — earlier two-run **raw** format (the single-run wire shape the runtime would
  emit). Kept as the capture-format reference.

Serve: `cd tools/trace-viewer && python3 -m http.server 8777`.

The prototype defines the **target render** and the **two file formats** (raw NDJSON per run,
merged tree.json). The runtime work below produces the raw format; the merge produces the
tree.

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

### 4.5 Merge step — start in the viewer (JS)

Keep the comparison **out of the runtime**. Load 2+ run NDJSONs → rebuild each tree → align
by `sig` → emit the merged `tree.json` the viewer already renders.

Reconvergence rule: after a fork, if every arm reaches a common downstream `sig` (or the same
final `ret`) → **green**; disjoint → **red**. (All three forks in the demo are red — a null
context doesn't take a side road, it never comes back to the same result.)

---

## 5. File formats

### Raw, per-run (runtime emits this) — NDJSON, one event per line

```
{"e":"in","k":"act","id":"a3","p":"a2","n":"variable.get","sig":"variable/get.cs","arg":{"name":"%user%"},"ts":0.011}
{"e":"out","id":"a3","ts":0.039,"ret":"User{…}","diff":[{"name":"%user%","to":"User{…}"}]}
```

`k` = goal/step/act/cs · `p` = parent id (rebuilds the tree) · `sig` = file:line · duration =
`out.ts − in.ts` · optional `ret`, `diff`, `warn`, `err`. Optional `branch` events carry the
condition + taken arm when `Trace.Fork` is annotated.

### Merged (derived by merge step) — nested tree.json

Node: `{kind, n, sig, ret?, warn?, children[]}`. Fork:
`{fork:true, q, at, reconverges:bool, arms:[{label, ret?, warn?, children[]}]}`. Root carries
`outcomes[]`. See `tools/trace-viewer/tree.json` for the worked example.

---

## 6. Build order

1. **Slice 1 — PLang-altitude sink.** `trace` concept + sink, `--trace` scoping, hooks in
   `Push` / `ExecuteAsync` / `DisposeAsync`. Real single-run NDJSON, no Fody. (~½ day,
   reuses all existing call data.) **This is the smallest end-to-end proof.**
2. **Slice 2 — JS merge.** Run with and without context, drop both NDJSONs in the viewer,
   forks appear automatically.
3. **Slice 3 — Fody weaving.** C# method depth under the PLang frames.
4. **Slice 4 — `Trace.Fork` enrichment** on the branches that matter.

---

## 7. Decisions for the architect

1. **`trace` as a first-class `app.X` concept** (folder `app/trace/`, `app.trace.current`)
   vs. folding the sink into the existing `CallStack.Flags` tier (alongside Timing/Diff/
   History). The concept is cleaner OBP; the flag is less surface. Recommendation: concept.
2. **Fody vs Harmony** for C# weaving (§4.3). Recommendation: Fody first.
3. **Merge location** — JS-in-viewer (proposed, zero runtime cost) vs a C# / PLang-goal tool
   that emits `tree.json` for CI/headless. Recommendation: JS first, port later if needed.
4. **Sig identity for alignment** — `file:line` is stable enough within a build but shifts as
   code moves. Is `file:methodName` a better merge key? Affects how robust cross-run
   alignment is.
5. **Retention / size** — NDJSON rotation cap, loop-folding of repeated `foreach` subtrees
   (hash subtree shape, render `×N`). Where does folding live — capture or viewer?

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
- **On, scoped:** bounded to the `--trace` subtree; append-only NDJSON; flat memory (no tree
  retained in RAM — the file is the record, the viewer rebuilds the tree).
