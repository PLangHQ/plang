# Coder summary — branch `goal-graph-singular`

## What this is
Graph → plang items: `goal`/`step`/`action`/`modifier` become `item.@this`, the three bespoke
collection classes delete, `Visibility`→`choice`, `.pr` read/write moves off reflection onto explicit
item Output + per-type readers. Architect plan: `.bot/goal-graph-singular/architect/` (`items-answer.md`,
`demolition-followup.md`, `binary-boundary-answer.md`).

## Landed so far (all pushed, green modulo proven-pre-existing failures)

### Increment-3 write side
`goal`/`step`/`action` write themselves via explicit `Output` (was: reflection). `Visibility` enum →
`choice<Visibility>` (self-serializes its symbol). Dead `InputParameters` deleted.

### Increment-3 read side (architect ruling 3a + binary-boundary A)
Per-type `ITypeReader`s (`goal`/`step`/`action` `serializer/Reader.cs`) walk the handed `IReader`; no
`Read*` statics. The **goal reader is the binary→json content boundary**: a `.pr`-from-disk load hands a
scalar `value.Reader`, so the goal parses its own json bytes once (`new json.Reader`) and `Walk`s in
place — step/action readers walk that reader nested. Architect ruled this is A (correct; content decode
belongs on the type), not a "never-new-a-reader" violation.

### Gate-2 Phase A — `actions.@this` DELETED (commit `f38bcdfc8`)
- `step.Actions`: `actions.@this` → `List<action>` + getter-loop back-ref (`a.Step ??= this`).
- `actions.Nest` re-homed onto **step** (`step.Nest`) — step owns its action chain; `steps.Nest` loops calling it.
- Recovery-chain + catalog params (`error.handle.Actions`, `validate.Actions`, `builder.actions` return,
  `if.Orchestrate`) `clr<actions.@this>` → `clr<List<action>>`.
- Obsolete `actions/serializer/Reader.cs` deleted (architect: not replaced — elements read via `action`'s reader).
- `StepActions` alias deleted (prod GlobalUsings + test Directory.Build.props repointed to `List<action>`).
- Migrated ~10 prod consumers + tests; deleted obsolete `ActionsReaderRoundTripTests` + the `.Value` test.

**Validated:** GroupModifiers 6/6 (Nest re-home), ActionsTests 19/19, StepTests 13/13, ErrorHandle 17/17
(recovery chain materializes via `clr<List<action>>`), Condition 10/10, GetActions 10/10. RunActionTests
1 pre-existing; ValidateActions 2 **baseline-confirmed pre-existing** (`c05b9ba6e`).

## What's left

### Gate-2 Phase B — `steps.@this` DELETE (next, bigger)
- `goal.Steps`: `steps.@this` → `List<step>` + getter-loop back-ref.
- Re-home per `items-answer.md`: `steps.RunAsync` (step loop) → **`goal.RunAsync`** absorbs it;
  `steps.Merge` → `goal.Merge` inlines; `steps.HasIndentedChildren` → goal; `steps.Nest` → goal loop.
- Handle `IContext`/`Context` (steps implemented `IContext`; consumers set `goal.Steps.Context`).
- Delete `GoalSteps` alias → `List<step>`. Flip the goal reader's `new steps.@this()` → `List<step>`.
- Run `Tools/ObpScan` on the re-homed members before pushing (architect).

### Then
Singular sweep (`LineNumber→Line`, `ActionName→Name`, `Steps→Step`, `Goals→Child`, namespaces
`goal.steps.step`→`goal.step`, wire keys) + `.pr` migration (hand-edit ~11 builder bootstrap `.pr`, rebuild rest).

## Open notes for architect (non-blocking)
- Recovery-chain actions read via **reflection** (`clr<List<action>>` list-host), not the `action` reader —
  functionally correct (ErrorHandle 17/17) and matches the pre-existing behavior, but a minor read-path
  inconsistency vs graph actions. Flag if the architect wants it unified to a plang `list<action>`.

## Code example (Phase A pattern — storage swap + getter-loop back-ref)
```csharp
// step/this.cs
private List<Action> _actions = new();
[Store, Debug, Default]
public List<Action> Actions
{
    get { foreach (var a in _actions) a.Step ??= this; return _actions; }   // back-ref, was the collection's indexer
    set => _actions = value ?? new();
}
public void Nest(module.list.@this modules) { /* re-homed from actions.@this — reshapes THIS step's chain */ }
```
