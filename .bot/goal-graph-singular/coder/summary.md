# Coder summary — branch `goal-graph-singular`

## Landed (all pushed, `847e9b11f`)

### Increment 3 + Gate-2 Phase A + §0 rename — DONE (green)
Graph self-wire (Output + readers), `Visibility`→`choice`, `actions.@this` deleted, namespace
`app.goal.steps.step`→`app.goal.step` / `…actions.action`→`app.goal.step.action`.

### Gate-2 Phase B — tree RUNTIME — DONE (green, validated)
- `step.list` / `action.list` nodes (`goal/step/list/`, `goal/step/action/list/`) own `Run`; implement
  `IReadOnlyList<T>` (reflection writes them as arrays, list-kind navigable, tests collection-init via
  helpers) + `Add` (construction only) + `IndexOf`. Backed by a reused-not-copied `List` (no ToList glue).
- `action.Child : step.list`; run chain `goal.Run→Step.Run→step.Run→Action.Run`; **fire in
  `action.list.Run`** (`IsCondition && truthy → Child.Run; break`). `condition.if.Run` evaluate-only.
- Deleted: `steps.@this`, `Decision`, `Orchestrate`, `skipBelowIndent`, `IsFirst`/`IsIfHead`.
- Coverage **derives test-side** (no runtime stamping).
- ~20 production + ~250 test consumers migrated. `RealGoalLoad` serializes the goal via its own Output
  (not clr-reflection) — fixed a key-mismatch that broke round-trips.

### Step 2 (partial) — child WIRE — DONE (additive, green round-trip)
Action reader `case "child"` (lazy step-reader breaks the ctor cycle) + action Output writes `child`
when non-empty. **Kept plural keys** (`steps`/`actions`) so disk `.pr` still reads — the singular-key
flip rides the `.pr` migration (step 4).

**Test state:** core suites green (StepTests, ActionsTests, GroupModifiers, GoalTests back to its 2
pre-existing). Remaining reds: pre-existing (StartGoal_Programmatic, ResolveValue_Full), deferred
(SnapshotWire — snapshot restore deferred), and **condition tests** — expected: their fixtures build
flat `if/elseif/else`, but the tree needs the branch bodies in `Child` (step 3 produces them).

## What's LEFT — steps 3–4 (the builder + migration; conditions not yet end-to-end)

The RUNTIME is correct (`action.list.Run` fires a truthy condition's `Child`). Conditions don't work
end-to-end yet because nothing PRODUCES `Child`-nested `.pr`:

3. **Builder** — two producers:
   - **Deterministic indent-fold** (C#, no eval risk): post-compile, fold a deeper-indented step into the
     preceding condition action's `Child`. Makes indented-block conditions work.
   - **LLM inline `if/elseif/else`** (the eval-risk piece): emit each branch body as `Child` steps with
     per-branch `text`. Schema + prompt + goldens in `os/system/builder/**`.
   - Condition test fixtures updated to build `Child` (or via the builder).
4. **`.pr` migration** — flip wire keys to singular (`step`/`action`/`name`/`child`, drop `indent`),
   hand-edit ~11 bootstrap `.pr` to `Child`-nest their conditions, rebuild the rest; verify the semantic
   round-trip + branch coverage (architect §14).

## Key files
- `PLang/app/goal/step/list/this.cs`, `goal/step/action/list/this.cs` — nodes.
- `goal/step/action/this.cs` — `Child`; `goal/step/action/serializer/Reader.cs` + `this.Item.cs` — child wire.
- `goal/this.cs`, `goal/step/this.cs` — Step/Action node props + parser.
- `module/action/condition/if.cs` — evaluate-only; `module/action/test/run.cs`+`discover.cs` — coverage.
