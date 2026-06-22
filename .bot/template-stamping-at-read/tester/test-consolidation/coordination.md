# Test-consolidation — tester/coder coordination

Both tester (`plang-tester-_interactive`) and coder (`plang-coder-_interactive`) are
migrating C# unit tests to the goal-run pattern (`Make.Goal → RealGoalLoad.ViaChannel →
RunGoalAsync`) on this branch. To surface collisions early, we claim modules here before
touching them. **Read this before editing a test module; append your claim.**

Guide: `Documentation/v0.2/writing-tests.md`. Method: convert observable behavior to
goal-run, keep the named floor (build-time / no-language-surface) as C# units, verify
parity with a cobertura line-set diff before deleting, then commit per module.

## Lanes

| module | owner | status | notes |
|--------|-------|--------|-------|
| variable | tester | **done** | 34→32, coverage parity proven. `VariableGoalRunTests` + `SetBindingContractTests` (floor). |
| condition | coder | in progress | coder migrated IfHandler/orchestration (commits `a805dbc3`, `2fd09f68`); tester stays out. |
| loop | coder | mostly migrated | tester stays out. |
| file | coder | partial | tester stays out. |
| list | tester | **done** | 39→41, coverage parity. `ListGoalRunTests` + floor `ListAddIdentityTests` (ref-identity) + `ListFlattenRecursionTests` (recursion needs raw nested seed). |
| math | tester | **claiming next** | fully raw, small. |

### Note for coder — wire flattens nested list literals
Seeding a nested list via `variable.set` through `RealGoalLoad.ViaChannel` (the .pr wire
read) arrives **flat** — `[1,[2,3]]` materializes as `[1,2,3]`. So flatten's recursive arm
is unreachable from a language-seeded list; only a raw `List<object?>` seed (direct
`Variable.Set`, no wire) preserves nesting. Flagging in case nested-list-through-wire is a
real round-trip bug rather than expected normalization.

## Conflict protocol
- One module = one owner at a time. Don't edit a module another bot owns.
- Shared infra (`PLang.Tests/Shared/Make.cs`, `RealGoalLoad.cs`, `TestApp.cs`): if you
  need a new helper, add — don't reshape existing signatures without a note here.
- If a cherry-pick / parallel edit conflicts, the second committer resolves and notes it
  in this file under "Observed conflicts" so we learn where the seams are.

## Observed conflicts
(none yet — variable cherry-picked onto the branch tip cleanly)
