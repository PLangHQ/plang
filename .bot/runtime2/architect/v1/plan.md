# Setup.goal — Handoff Spec for Coder

## What This Is

Setup goals run once-per-step at app startup. Every PLang app runs its setup goals before the requested goal. Steps are tracked persistently in system.sqlite — each step runs exactly once in the app's lifetime. If the developer adds a new step later, only the new step runs. If the developer changes a step (hash changes), it re-runs.

## The Object Model

```
Engine
  └── Goals
        └── Setup                              ← NEW object (replaces IEnumerable<Goal> filter)
              ├── Goals: IEnumerable<Goal>      ← filtered from EngineGoals, ordered (Setup first, then alpha)
              ├── Executions                    ← NEW smart collection, one row per step in system.sqlite
              │     ├── Contains(step) → bool   ← checks step.Hash against table
              │     └── Add(step, error?)       ← inserts row with hash + metadata, persists immediately
              └── RunAsync(context)             ← iterates goals, stamps context.Setup

PLangContext
  └── Setup: Setup?                            ← NEW property, set during setup execution, null otherwise
```

## Execution Flow

### App start (normal run):
```
engine.Goals.Setup.RunAsync(context)    // setup always first
engine.RunGoalAsync(goalName, context)  // then the requested goal
```

### `plang setup` (explicit):
```
engine.Goals.Setup.RunAsync(context)    // only setup, no main goal after
```

### Inside Setup.RunAsync:
1. Set `context.Setup = this`
2. Iterate setup goals (ordered: goal named "Setup" first, then alphabetical)
3. Skip goals with dynamic datasource (`%` in datasource name — if applicable in runtime2)
4. For each goal: `goal.RunAsync(engine, context, ct)`
5. Clear `context.Setup = null` when done

### Inside Steps iteration (the run-once check):
```csharp
// Only when context.Setup is present:
if (context.Setup?.Executions.Contains(step) == true)
    continue;

// ... run step normally ...

// Record after execution (even on tolerated errors):
context.Setup?.Executions.Add(step, error);
```

When `context.Setup` is null (normal execution), this is a null check that short-circuits. Zero setup awareness in the normal path.

### Goal.call propagation:
Context carries `Setup` through the parent chain. Any goal called from within setup execution inherits the setup context. No special code in goal.call — it just works because the context propagates. Steps in called goals get the same run-once treatment.

### Regular RunGoalAsync:
Setup goals should be **excluded** from regular goal lookup. A developer cannot accidentally call a setup goal from normal code. Setup goals are only reachable through `engine.Goals.Setup.RunAsync()`.

## Executions Table (system.sqlite)

One row per executed step. Developer can delete a row to force re-run.

```sql
CREATE TABLE IF NOT EXISTS SetupExecutions (
    Hash        TEXT PRIMARY KEY,
    GoalPath    TEXT NOT NULL,
    StepIndex   INT NOT NULL,
    StepText    TEXT NOT NULL,
    ExecutedAt  TEXT NOT NULL,
    Error       TEXT NULL
);
```

- **Hash** — `step.Hash` (already exists on Step model, computed at build time)
- **GoalPath** — for human readability when inspecting the table
- **StepIndex** — for human readability
- **StepText** — for human readability
- **ExecutedAt** — ISO 8601 UTC timestamp
- **Error** — null on success, error detail when failure was tolerated

### Self-bootstrapping:
Executions must create its own table on first access — before any setup step runs. This is infrastructure, not a setup step. No chicken-and-egg.

## Step.Hash

**Already exists** on the Step model (`PLang/App/Engine/Goals/Goal/Steps/Step/this.cs:34`).

The hash composition must be: `hash(goalPath + compiledAction)` — where compiledAction is the module + method + parameters from the built .pr. This is a **builder-side** change. The coder should verify what the current hash contains and adjust if needed. The hash must NOT include step index (reordering should not trigger re-runs).

If the builder already computes the hash correctly, no change needed. If not, the builder needs updating.

## Key Design Decisions (Already Made)

1. **Setup is an object, not a filtered list.** `engine.Goals.Setup` returns a `Setup` object with identity and behavior, not `IEnumerable<Goal>`.

2. **Executions is a smart collection.** Wraps a sqlite table, not a serialized dictionary. One row per step. Loads lazily on first access.

3. **context.Setup propagates through goal.call.** Any step reachable from setup execution gets run-once semantics — direct steps AND steps in called goals at any depth.

4. **Step is readonly, shared between threads.** No mutable state on Step. The execution tracking is in Executions (persistent) and context.Setup (per-execution).

5. **No procedural verbs on Setup.** `Executions.Contains(step)` and `Executions.Add(step)` — collection operations, not `Setup.HasExecuted()` / `Setup.Record()`.

6. **One row per step in sqlite.** Developer can delete a row to force re-run. Much better than runtime1's serialized dictionary blob.

7. **Error tolerance is deferred.** For now, note that setup has different error tolerance than normal execution (e.g., CREATE TABLE on existing table is fine). The error info is stored in the Executions row. The policy for which errors are tolerable is future work.

8. **Ordering:** Goal named "Setup" first, then alphabetical. Developer controls order through filename (e.g., `1_database.goal`, `2_seed.goal`).

## Existing Code to Change

### `PLang/App/Engine/Goals/this.cs` (EngineGoals)
- **Line 186**: `Setup` property currently returns `IEnumerable<Goal>`. Change to return a `Setup` object.
- Regular goal lookup (`Get`, `GetAsync`, `Run`) should **exclude** setup goals — setup goals are only reachable through `Setup.RunAsync()`.

### `PLang/App/Engine/Goals/Goal/Methods.cs` (Goal.RunAsync)
- **Lines 57-70**: Goal currently iterates steps directly. This violates OBP rule 5 ("Collections are smart wrappers... Parents delegate — they never iterate directly"). Steps should own a `RunAsync` method. Goal.RunAsync should delegate to `Steps.RunAsync(engine, context, ct)`. The setup check goes inside Steps.RunAsync.

### `PLang/App/Engine/Goals/Goal/Steps/this.cs` (Steps)
- Currently only has `Load`. Needs `RunAsync(engine, context, ct)` that owns the step iteration loop (moved from Goal.RunAsync). The setup run-once check goes here.

### `PLang/App/Engine/Context/PLangContext.cs`
- Add `Setup` property (nullable). Must be included in `Clone()` and `CreateChild()` methods — this is how goal.call propagation works.
- **Clone/Copy family audit**: `Clone()` (line 193) and `CreateChild()` (line 184) both need the new property.

### Entry point (wherever the app boots)
- Call `engine.Goals.Setup.RunAsync(context)` before the main goal.
- When the requested goal IS "setup", only run setup — no main goal after.

## New Code to Create

### `PLang/App/Engine/Goals/Setup/this.cs`
The Setup class. Owns:
- `Goals` — filtered + ordered setup goals (lazy)
- `Executions` — the smart collection (lazy)
- `RunAsync(context)` — stamps context.Setup, iterates goals, clears context.Setup

### `PLang/App/Engine/Goals/Setup/Executions.cs`
The Executions smart collection. Owns:
- Self-bootstrapping (CREATE TABLE IF NOT EXISTS on first access)
- `Contains(step)` — sqlite query by hash
- `Add(step, error?)` — sqlite insert with step metadata
- Lazy loading from system.sqlite
- Navigates to engine for sqlite access

## Concurrency Note

Not a concern. The console that starts a webserver runs setup at startup. Web requests never touch setup. Setup runs once per app start, synchronously, before anything else.

## Testing

- C# unit tests for Setup, Executions (mock sqlite or use in-memory)
- PLang .goal test: create a Setup.goal with steps, run it twice, verify steps only execute once
- PLang .goal test: add a step after first run, verify only the new step executes
- PLang .goal test: Setup.goal calls another goal, verify called goal's steps are also tracked
