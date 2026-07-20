# Coder summary — branch `goal-graph-singular`

## Where it stands (latest: `72d72f438`)

Graph → plang items + the **tree** model. Landed in order:

### Increment 3 — graph self-wire (DONE, green)
`goal`/`step`/`action` write themselves (`Output`) + per-type `ITypeReader`s; `Visibility`→`choice`;
`InputParameters` deleted; goal reader is the binary→json content boundary. Architect-ruled A.

### Gate-2 Phase A — `actions.@this` deleted (DONE, green)
`step.Actions`→`List<action>`, `Nest` re-homed to step, recovery-chain params → `clr<List<action>>`.

### §0 namespace rename (DONE, green, `50f6711de`)
`app.goal.steps.step`→`app.goal.step`, `…actions.action`→`app.goal.step.action`; folders moved up;
generator + ~100 refs updated. Wire keys still plural (flip pending with the reader/Output tree work).

### Gate-2 Phase B tree RUNTIME (DONE — **PLang compiles clean**, `72d72f438`)
The architect's tree design (`phaseB-tree-code.md`), built:
- `step.list` (`goal/step/list/`) + `action.list` (`goal/step/action/list/`) — minimal nodes owning `Run`,
  `[i]`/`list`/`Count` (+ `IndexOf` on action). Hold `IReadOnlyList` (no `.ToList()` glue — Ingi's call).
- `action.Child : step.list` (branch body). Run chain: `goal.Run`→`Step.Run`→`step.Run`→`Action.Run`;
  **fire lives in `action.list.Run`** (`IsCondition && truthy → Child.Run; break`), no `Handled` branch signal.
- `condition.if.Run` collapsed to evaluate-only. **Deleted:** `steps.@this`, `Decision`, `if.Orchestrate`,
  `skipBelowIndent`, `IsFirst`/`IsIfHead`.
- Coverage **derives test-side** (no runtime branch-stamping) — `run.cs` observer keys off `IsCondition`+truthy
  + `action.list.IndexOf`; `discover` walks condition actions for the declared chain (no `Decision`).
- `goal.Steps`→`goal.Step`, `step.Actions`→`step.Action`; ~20 production consumers migrated (foreach→`.list`,
  index→`[i]`, immutable-node copies via `new list(node.list)`); `.RunAsync`→`.Run`.

## What's LEFT (the tree is not yet end-to-end)

**The wire is still flat** (plural keys, no `child`, `indent` present) while the runtime expects the tree.
So conditionals/sub-steps DON'T run correctly until the wire + builder + `.pr` land together:

1. **Test compilation** — ~194 mechanical consumer migrations in test files (`.Steps`/`.Actions` →
   `.Step`/`.Action`, `GoalSteps`/`StepActions` initializers → node construction, `.RunAsync`→`.Run`).
   Unblocks the C# unit suites (which build goals via `Make` — validate the tree runtime WITHOUT the wire).
2. **Readers + Output** — action reader `child` recursion; step `Output` writes `child`, drops `indent`;
   singular wire keys (`step`/`action`/`name`/`child`); born-with backrefs via `ReadContext` (§5/§6).
3. **Builder** (`os/system/builder/**` in PLang) — deterministic indent-fold + LLM inline `if/elseif/else`
   with per-branch `text` (the eval-risk piece).
4. **`.pr` migration** — hand-edit ~11 bootstrap `.pr` (Ingi-permitted), rebuild the rest; verify the
   semantic round-trip + branch coverage (architect §14 acceptance).

## Key files (this branch)
- `PLang/app/goal/step/list/this.cs`, `PLang/app/goal/step/action/list/this.cs` — the nodes.
- `PLang/app/goal/step/action/this.cs` — `action.Child`.
- `PLang/app/goal/this.cs`, `goal/step/this.cs` — `Step`/`Action` node properties + parser rework.
- `PLang/app/module/action/condition/if.cs` — evaluate-only.
- `PLang/app/module/action/test/run.cs` + `test/discover.cs` — coverage derivation.

## Note
The C# unit tests can validate the tree runtime directly (Make-built goals, no wire) once test files
compile (step 1). The wire flip (steps 2-4) is the coordinated big-bang — no green intermediate, verified
by round-trip at the end.
