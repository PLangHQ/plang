## 2026-06-26 — fork graph model settled, example handed to codeanalyzer

Refined the execution-trace design with Ingi. Scope corrected: this is not a "diff two runs of one action" tool — it captures **every C# method the app runs**, folds **all runs** into one accumulating graph, and surfaces the **forks** (where runs diverge). The graph saturates — it's bounded by the app's static reachable call graph, not by run count, so "all runs" is tractable.

Two model decisions, both settled:
- **Call edges** (`A → B` = A called B), not execution-sequence edges. Sequence edges make every shared utility a fake merge and every multi-callee method a fake fork once everything is woven.
- **Fork = "callees differ across runs,"** measured by distinct-run-counts on nodes and edges: node `F` forks iff some out-edge's `runCount` < `F.runCount`. No run identity retained. Exact under loops (dedup within a run). Plain out-degree ≥ 2 was rejected — it would light up nearly every method.

v1 is **forks only**; merges (arms rejoining) and "which run took the arm" are deliberate v2.

Deliverables for codeanalyzer (to render in the web UI):
- `fork-graph-model.md` — the settled model, the call-edge NDJSON format, the build/fork rule, the worked example, and what the UI should show. Names the change from the current `canvas.html` sequence model.
- `example.ndjson` — fabricated 4-run capture of `Start` reading `%user%` (warm / cold-with-context / cold-no-context / warm). Produces exactly three forks: `data.As`, `data.Wire.ReadBody`, `data.Wire.ReadSignatureLayer`. Table of expected fork runCounts included as an acceptance check.

Still open (deferred, from the canonical doc §7): Fody vs Harmony for weaving (leaning Harmony now that it's app-wide — zero baseline cost), arming model, and how a user requests "the same action under N conditions." Not blocking the UI demo.

Stage status: design conversation, no coder stages carved yet.

| Item | File | Status |
|------|------|--------|
| Fork graph model | [fork-graph-model.md](fork-graph-model.md) | complete |
| Example capture data | [example.ndjson](example.ndjson) | complete |
| Coder stages | — | not started (UI demo first) |
