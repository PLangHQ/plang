# v2 Plan: Builder Trace Viewer Redesign

## Problem

The current trace viewer (`system/builder/web/index.html`) shows a flat sidebar of goal names (Build, BuildGoal, ApplyStep, ValidateBuildResponse, BuildStep) with no hierarchy. You can't tell:
- Which user .goal file was being built
- What triggered a BuildStep (was it low confidence? validation failure?)
- The order things happened for a given step
- Which build run a trace belongs to

## Design: Flow-Oriented Trace

### New trace data format

One JSON file per build run instead of one per goal invocation. Structured as a tree of the user's goals:

```json
{
  "buildRunId": "639116909...",
  "timestamp": "2026-04-13T13:09:00Z",
  "path": ".",
  "goalCount": 3,
  "goals": [
    {
      "name": "Start",
      "path": "Start.goal",
      "status": "success",
      "timestamp": "2026-04-13T13:09:01Z",
      "duration": "2.3s",
      "buildGoal": {
        "system": "...(the BuildGoal.llm prompt)...",
        "user": "...(the goal text formatted for LLM)...",
        "response": { "description": "...", "steps": [...] }
      },
      "steps": [
        {
          "index": 0,
          "text": "write out \"hello\"",
          "finalLevel": "high",
          "finalConfidence": 95,
          "actions": [{"module":"output","action":"write",...}],
          "flow": [
            { "phase": "buildGoal", "level": "high", "confidence": 95 },
            { "phase": "validate", "status": "pass" },
            { "phase": "applyStep", "status": "merged" }
          ]
        },
        {
          "index": 1,
          "text": "llm.query system=%prompt%, user=%input%, ...",
          "finalLevel": "high",
          "finalConfidence": 88,
          "actions": [{"module":"llm","action":"query",...}],
          "flow": [
            { "phase": "buildGoal", "level": "medium", "confidence": 55 },
            { "phase": "validate", "status": "error", "error": "unknown param Schema vs scheme" },
            { "phase": "buildStep", "system": "...", "user": "...", "response": {...} },
            { "phase": "validate", "status": "pass" },
            { "phase": "applyStep", "status": "merged" }
          ]
        }
      ]
    },
    {
      "name": "ValidateBuildResponse",
      "path": "ValidateBuildResponse.goal",
      "status": "success",
      ...
    }
  ]
}
```

### New UI layout

**Left sidebar**: Build run selector (date/time) + list of user goals being built (not builder internals). Each goal shows step count and status (green check / yellow warning / red error).

**Main panel**: When you select a goal, you see a vertical flow:

```
Start.goal (3 steps, built in 2.3s)
  
[Step 0] write out "hello"
  BuildGoal: high 95% -> Validate: pass -> Apply: merged
  
[Step 1] llm.query system=%prompt%, ...
  BuildGoal: medium 55% -> Validate: ERROR "unknown param"
    -> BuildStep (detail pass)
       [System Prompt] [User Prompt] [LLM Response]  <- expandable
    -> Validate: pass -> Apply: merged

[Step 2] call ValidateBuildResponse, on error call LlmFixer
  BuildGoal: high 85% -> Validate: pass -> Apply: merged
```

Each step is a horizontal "pipeline" showing what happened. Green phases are collapsed. Yellow/red phases auto-expand so you can see what went wrong.

For any phase that involves an LLM call (buildGoal, buildStep), clicking it expands the system/user/response — same good format as today.

### Implementation plan

**Phase 1: Sample data (manual)** — Create a sample trace JSON file by hand using real data from the existing traces. This is the "design spec" for the format.

**Phase 2: New HTML viewer** — Rewrite `index.html` to render the new format. The viewer reads from `/.build/traces/build-run.json` (the latest run).

**Phase 3: Iterate on UI** — Get feedback, adjust.

**Phase 4: Update .goal files** — Once the UI is right, modify `Build.goal`, `BuildGoal.goal`, `BuildStep.goal`, `ApplyStep.goal` to produce the new format instead of per-goal files.

### What changes in each file (Phase 4, for reference)

- `Build.goal`: Create the root build-run object, save single file at end
- `BuildGoal.goal`: Add goal entry to build-run, record each step's flow
- `ApplyStep.goal`: Record validate/merge result in step flow
- `BuildStep.goal`: Record detail pass in step flow
- `ValidateBuildResponse.goal`: Record validation result in step flow

### Out of scope

- Multiple concurrent build run support (can add later)
- Filtering/search within traces
- Diffing between runs
