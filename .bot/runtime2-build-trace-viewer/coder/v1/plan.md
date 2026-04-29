# Coder v1 — Plan: Trace viewer navigation grouped by .goal file path

## Goal

Let Ingi see what the LLM is being sent, grouped per `.goal` file rather than as a flat list of ~20 goal traces. Public and private goals from the same file should appear together as a sub-tree under the file path.

## Problem in the current viewer

- Trace files live at `.build/traces/<ticks>_<GoalName>.json` and contain only `{id, goal, timestamp, pass1, detailPasses}`. **There is no `path` field**, so the viewer can't know which `.goal` file a trace came from.
- Sidebar lists every trace as an independent row. A rebuild of `Start.goal` produces `Start.json` and `Hello2.json` as two unrelated entries.
- No build datetime is visible in the sidebar.

## Scope (v1)

Three surgical changes. Nothing else.

### 1. Backend — enrich the trace object

**File:** `system/builder/BuildGoal.goal`, step in `BuildGoalCore` that builds `%trace%` (currently line 26).

**Before:**
```
- set %trace% = {"id": "%traceId%", "goal": "%goal.Name%", "timestamp": "%Now%", "pass1": {...}, "detailPasses": []}, type=json
```

**After:** add two fields — `path` and `visibility`:
```
- set %trace% = {"id": "%traceId%", "goal": "%goal.Name%", "path": "%goal.Path%", "visibility": "%goal.Visibility%", "timestamp": "%Now%", "pass1": {...}, "detailPasses": []}, type=json
```

Rationale: all goals (public + private) in the same `.goal` file share the same `Goal.Path`, since `Path` is the file path per `Documentation/v0.2/goals-steps.md`. `Visibility` is already on Goal (`Public=1`, `Private=0` — per CLAUDE.md). That gives the viewer everything it needs: group by `path`, split public vs private under each group.

**Verification before committing:** rebuild `Start.goal`, read the resulting `.build/traces/*_Start.json` and `*_Hello2.json`, confirm both have `path: "Start.goal"` and correct `visibility`.

### 2. Server — cap listing at top 20 newest

**File:** `system/builder/web/server.py`.

`send_trace_list()` currently returns every `*.json` file, sorted alphabetically. Change to:

- Sort by filename descending (tick prefix sorts chronologically — newest first).
- Return the top 20.
- Skip legacy un-prefixed files (`Start.json`, `Nuget.json`) so they don't crowd out real traces. They'll still be served by `/api/traces/<name>` if requested directly but won't appear in the list.

Files on disk are never deleted — trace accumulation is kept as you asked; the server just filters the view.

### 3. Frontend — tree sidebar grouped by path

**File:** `system/builder/web/index.html`.

Current sidebar: flat list of goals. Replace with a 3-level tree built from the fetched traces.

```
📁 Start.goal                                      last build 19:11:34
  ├─ Start (pub)                                            2 traces ▸
  │   • 2026-04-21 19:11:31  ✓
  │   • 2026-04-21 17:42:14  ✓
  └─ Hello2 (priv)                                          1 trace  ▸
      • 2026-04-21 19:11:34  ✓
```

Rules:
- Group traces by `path`. Each file row shows the path and the newest trace's timestamp.
- Within a file, public goals first, private goals below.
- Each goal is expandable to reveal its history (descending by timestamp).
- Clicking a file row: expand/collapse that file's tree.
- Clicking a goal row: expand/collapse its history, and select the most recent trace for viewing.
- Clicking a specific timestamp: render that exact trace in the main panel.
- On first load: auto-select the newest trace in the newest file.
- Traces missing `path` (legacy traces built before change #1) go into an "unknown" bucket at the bottom so we can still inspect old data during the rollout.

Main panel: no change in v1. Still renders a single trace.

## Non-goals (v1)

- No "file overview" main panel — clicking a file just expands; clicking a trace leaf renders that trace exactly as the main panel renders today.
- No history truncation inside the viewer beyond the server's top-20 cap.
- No changes to what's *inside* a trace view (prompts, steps, responses) — those remain untouched.
- No filtering, search, or time-range controls. Iterate in v2 if needed.
- No change to `saveTrace` behavior on disk — traces still accumulate forever; you can prune manually.

## Risks

- **R1 — `%goal.Path%` not available in string interpolation in `set ... type=json`.** If the `.pr` serializes `path` as empty, I'll need to move the field through a separate `set %path% = %goal.Path%` first. Will verify after rebuilding.
- **R2 — Visibility serialized as enum vs int.** `"%goal.Visibility%"` may write `"Public"` or `"1"`. Viewer will accept both (`== "Public" || == "1" || == 1`).
- **R3 — Tick-prefix filename sort isn't universal.** Old `Start.json` and `Nuget.json` without a tick prefix sort differently. I'm skipping them from the list — safe.

## Deliverables

- `system/builder/BuildGoal.goal` — trace enrichment
- `system/builder/web/server.py` — top-20 list
- `system/builder/web/index.html` — tree navigation with datetime
- `.bot/runtime2-build-trace-viewer/coder/v1/summary.md` — written at end
- `.bot/runtime2-build-trace-viewer/coder/v1/changes.patch` — written at end

## Order of work

1. Change `BuildGoal.goal`. Run `plang build` on `Start.goal` only. Read the two resulting traces. Confirm `path` + `visibility` land correctly.
2. Update `server.py`. Restart it. Hit `/api/traces` and confirm list is capped to 20 newest.
3. Rewrite sidebar in `index.html`. Smoke in browser. Confirm tree expands and trace selection works.
4. Commit + push.

## Open question

None — answers from Ingi cover Q1 (accumulate + server caps at 20), Q2 (subtree nav), Q3 (sub-goals always same file). Ready to proceed after approval.
