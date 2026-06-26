# Execution trace — the fork graph model (settled)

**Branch:** `execution-trace`
**For:** codeanalyzer — build the web UI that renders this, using the example below.
**Supersedes** the sequence/overlay framing in `Documentation/Runtime2/execution-trace-design.md` §2/§3.1/§5. The "why" and the `trace` concept / Fody-vs-Harmony / arming sections of that doc still stand; only the **edge meaning** and the **fork definition** changed.

> **Codeanalyzer — you own the render.** Everything below (the NDJSON shape, colors, layout) is a suggestion precise enough to build against, not a spec to obey to the letter. The two things that are *not* yours to change are the model decisions in §1 — call edges and the distinct-run-count fork rule. Pick whatever viewer tech and layout reads best.

---

## 1. The model (this is the settled part)

The goal: capture **every C# method the app executes**, fold all runs into **one graph**, and make the **forks** jump out. A fork is where runs that shared a path start calling different things — that is where behavior diverges, and it is usually the only thing worth looking at. The deeper the call tree goes the more the runs should agree (OBP funnels everything to shared leaves), so the trunk is boring on purpose and a fork is the event.

Three decisions:

**Edges are call edges, not execution order.** `A → B` means **A called B** (parent → child in the call tree). It does *not* mean "B ran right after A." Execution-order edges turn every shared utility (`Data..ctor`, etc.) into a node with a hundred predecessors — fake merges — and make every method with two callees look like a fork. Call edges give one clean question per node: *did its callees change between runs?*

**A fork is "callees differ across runs."** Not out-degree ≥ 2. A method that calls `B`, `C`, `D` in sequence on *every* run is trunk, not a fork — its callee set never changes. A method that calls the typed reader in one run and the json parser in another **is** the fork. This is the only definition that survives weaving every method.

**Measure it with distinct-run-counts — no run identity retained.** On each node and each edge in the accumulated graph, keep a count of **how many distinct runs touched it**. Then:

> **Node `F` is a fork ⟺ some out-edge of `F` has `runCount` < `F.runCount`.**
> (Some runs reached `F` and went somewhere other than that edge.)

Two integers per node and per edge. No set of run-ids is stored. This is exact even with loops, because a method called five times inside one `foreach` still counts as **one** run touching that edge (dedup within the run, then increment). That is the whole reason it's run-*count*, not call-count.

**v1 is forks only.** Merges (two arms of a fork rejoining) are a harder computation — find the fork, follow both arms, detect the common descendant — and you said forks are what you're after. In-degree ≥ 2 is *not* a meaningful merge (it's just a popular method), so don't flag it yet. The picture will still *show* arms peeling off and rejoining the trunk; we just don't label the rejoin in v1.

**No run identity in the stored graph.** "Which run took the weird arm" is a deliberate v2 toggle. If you want it later, edges grow a bounded sample of run-ids; the model above doesn't change.

---

## 2. Capture format (call-edge NDJSON)

One file per capture, one line per event, execution order. This is what a real Fody/Harmony weave would emit on method entry/exit; for now it's hand-written (see `example.ndjson`).

```
{"e":"meta","title":"...","note":"..."}
{"e":"run","run":"A","label":"warm — typed in memory","color":"#7aa2f7"}
{"e":"enter","run":"A","id":4,"parent":3,"sig":"data.As","n":"Data.As<User>"}
{"e":"ret","run":"A","id":4,"ret":"User{ id:7 }  ✓ typed, from memory"}
```

- `enter` — one method entry. `id` = a per-run frame id; `parent` = the calling frame's id (`null` at the root). `sig` = **bubble identity** (`Namespace.Method`, stable across edits — never `file:line`, which would shift on every code move and invent phantom forks). `n` = display label.
- `ret` — a frame's outcome; `ret` text is the return shown at the node. Only the frames whose outcome matters need one.
- `run` — declares a run (id, label, lane color). Run tags exist in the **stream** only so the merger can count distinct runs; the accumulated graph keeps the counts, not the runs.

The edge is `sig[parent] → sig[id]`. The merger builds a per-run `id → sig` map, emits each distinct `(parentSig, sig)` pair once per run, and increments that edge's and node's `runCount`.

---

## 3. Building the graph from the stream

1. **Nodes** — one per distinct `sig`. Keep `runCount` (distinct runs that entered it), display `n`, and the set of `ret` strings seen (the outcomes).
2. **Edges** — one per distinct `(fromSig, toSig)`. Keep `runCount` (distinct runs that traversed it). Optionally keep `callCount` (total traversals) for edge thickness.
3. **Per-run dedup** — within a single run, the first time you see a node or edge, mark it; bump its `runCount` once at run end. Repeats inside the run (loops, retries) don't re-count.
4. **Forks** — `F` is a fork iff any out-edge `runCount` < `F.runCount`. Its **arms** are its out-edges; its **outcomes** are the `ret` strings reachable down each arm.
5. **Layout** — layered left → right by call depth (longest path from root). Root far left, leaves/returns far right. A node's column = its depth, so a child always sits right of its parent.

---

## 4. Worked example — `example.ndjson`

A tiny PLang program runs four times: `Start` reads `%user%` then writes a greeting. The read takes a different C# path depending on whether the value is already typed in memory, and (when it isn't) whether the actor context is present.

| Run | Condition | Read path | Outcome |
|-----|-----------|-----------|---------|
| A | warm — typed in memory | `Data.As` → `type.Match` | `User{…}` ✓ typed, from memory |
| B | cold, context present | `Data.As` → `Wire.Read` → `ReadBody` → **typed reader** → `SignatureLayer` → **verify** → `type.Build` → `Convert` | `User{…}` ✓ typed, verified |
| C | cold, context null | `Data.As` → `Wire.Read` → `ReadBody` → **json.Parse** → `SignatureLayer` (no verify) → `type.Judge` → `new item.source` | `item.source` ⚠ untyped, unverified |
| D | warm — typed in memory | same as A | `User{…}` ✓ typed, from memory |

All four share the trunk `goal.call → call.Execute → variable.get → Data.As`, and all four rejoin at `call.Execute → output.write → Channel.WriteText` (the second step — the arms reconverge on the trunk's tail).

**Expected forks** (your renderer should land exactly these three — use them as an acceptance check):

| Fork node | node runCount | out-edges (→ callee : edge runCount) | why it forks |
|-----------|:---:|---|---|
| `data.As` (`Data.As<User>`) | 4 | `→ type.Match : 2` · `→ data.Wire.Read : 2` · `→ type.Build : 1` · `→ type.Judge : 1` | warm vs cold, then verified vs unverified — every arm < 4 |
| `data.Wire.ReadBody` | 2 | `→ type.Readers.Typed : 1` · `→ serializer.json.Parse : 1` | the context-null fork at the C# level — typed reader vs raw json |
| `data.Wire.ReadSignatureLayer` | 2 | `→ data.signature.Verify : 1` | verify fires with context, is skipped without — 1 < 2 |

Everything else is trunk: `goal.call → call.Execute → variable.get → data.As` all have runCount 4 and a single dominant callee, and the `output.write → Channel.WriteText` tail is shared by all four.

---

## 5. What the UI should show

- **Layered left → right** by call depth. Root (`goal.call`) far left; outcomes (`type.Convert`, `new item.source`, `Channel.WriteText`) far right.
- **Trunk neutral** (gray). **Fork nodes ringed** (purple) — the three above. A node's ring is the only thing the eye should have to find.
- **Arms colored by run** for this demo (the run tags are in the stream) — the blue warm lane, the green verified lane, the orange unverified lane peeling off `Data.As` and the cold lanes splitting again at `ReadBody`. In production there are no run colors, only fork rings + edge weight; the demo coloring is just to make the divergence legible.
- **Edge thickness = runCount** (or callCount) so the trunk reads as a thick spine and rare arms as thin threads.
- **Hover a node** → its `sig`, `runCount`, and the outcome(s) reachable from it. **Hover/click a fork** → its arms side by side with the differing outcomes (the "here are the two outcomes" view).
- **Default lens = forks.** With every method woven the full graph is large; the home view should collapse the trunk and show fork nodes + their arms, expanding into the trunk on demand.

---

## 6. What changes from the current prototype

`tools/trace-viewer/canvas.html` + `flow.ndjson` draw **sequence edges** (consecutive method entries joined) and treat out-degree as the fork. That model breaks once every method is woven (every node becomes a fork, shared utilities sprout fake merges). The change:

- Switch the edge meaning to **call edges** (`parent → child`), using the `enter`/`parent` fields in the new format.
- Compute forks by the **distinct-run-count rule** (§1), not out-degree.
- Consume `example.ndjson` (this format) instead of the old `flow.ndjson` (the old file can stay as a reference of the superseded model).

The horizontal layered layout and the fork/trunk aesthetic from the prototype are right — keep them. Only the graph-construction underneath changes.
