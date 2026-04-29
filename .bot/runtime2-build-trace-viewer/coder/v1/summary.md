# Coder v1 — Trace viewer navigation grouped by .goal file path

## What this is

Ingi's main visibility problem: the builder is non-deterministic, and when `.goal` files don't build the way he expects, he can't see *what the LLM actually got*. A web-based trace viewer already existed (`system/builder/web/`), serving trace JSON dropped by the builder after each LLM pass — but its sidebar was a flat list of every trace, with no file grouping, no build datetime, and no way to tell which traces belonged together.

This v1 adds the minimal scaffolding needed to use traces as a real debugging surface: group by `.goal` file path, nest public and private goals inside the file, show the build time, and keep the history so you can compare consecutive builds of the same goal.

## What was done

Three surgical changes:

### 1. Enriched the trace object (`system/builder/BuildGoal.goal`)

`BuildGoalCore` writes a `%trace%` JSON object to `.build/traces/<ticks>_<goal>.json`. Added `path` and `visibility` fields so the viewer can group by file:

```
set %trace% = {"id": "%traceId%", "goal": "%goal.Name%", "path": "%goal.Path%",
               "visibility": "%goal.Visibility%", "timestamp": "%Now%", ...}
```

**Important gotcha:** Rebuilding `system/builder/BuildGoal.goal` itself to pick up the change hit LLM drift — the builder's own `BuildGoal` top-level goal came back with a wrong description, wrong throw parameter shape, and flipped visibility flags, which then crashed subsequent builds with `ValidationError`. After reverting `buildgoal.pr` and, per Ingi's call, hand-editing *just* the one step in that `.pr` to match the new `.goal`, the pipeline ran cleanly. This is a breadcrumb worth keeping in mind: the builder rebuilding itself is the riskiest build in the system.

Result, verified via fresh traces: `path: "/Start.goal"`, `visibility: "public"` (and `"private"` for Hello2).

### 2. Server top-20 filter (`system/builder/web/server.py`)

`send_trace_list()` now:
- Filters to tick-prefixed filenames only (skips legacy `Start.json`, `Nuget.json`, etc.).
- Sorts filenames in reverse — the tick prefix is chronological, so reverse-sort is newest-first.
- Caps at `TRACE_LIMIT = 20`.

Files on disk are untouched; the cap lives in the server only.

### 3. Tree sidebar (`system/builder/web/index.html`)

Replaced the flat `#goal-list` with a file → goal → trace tree:

- Files are ordered by most-recent build descending; a `(unknown)` bucket sinks to the bottom for legacy traces without `path`.
- Inside each file, public goals render before private, each with a `pub`/`priv` chip.
- Each goal has its own expandable trace history, sorted newest-first.
- Selection state is per-trace (`selectedTraceId`); expansion state is tracked in two `Set`s (`expandedFiles`, `expandedGoals`) so the tree keeps its shape across 3-second polls.
- `renderGoalDetail(idx)` → `renderEntry(entry)` — same main-panel rendering, just takes the entry object directly.

## Code example

Tree build (the whole point of this session):

```js
function buildFileTree(entries) {
  const byPath = new Map();
  for (const e of entries) {
    const path = e.path || '(unknown)';
    if (!byPath.has(path)) byPath.set(path, new Map());
    const goalMap = byPath.get(path);
    if (!goalMap.has(e.name)) goalMap.set(e.name, { name: e.name, visibility: e.visibility, traces: [] });
    goalMap.get(e.name).traces.push(e);
  }
  // ...split into publicGoals/privateGoals, sort each goal's traces newest-first,
  // record latestTs per file, push (unknown) to bottom.
}
```

That's the whole shape. Everything else is presentation.

## Notes

- **Visibility may be serialized as the enum name or an int**, depending on the PLang variable pipeline — the viewer's `normalizeVisibility()` accepts `"public"|"Public"|1|"1"` for public and the mirror for private.
- **Hand-edited `.pr` remains as-is.** A future clean rebuild of `system/builder/BuildGoal.goal` should produce the same step output; if it drifts the fix belongs in `BuildGoal.llm`, not the runtime.
- **No test was added.** The viewer is a dev tool; its "tests" are Ingi looking at the page and saying the tree makes sense.
