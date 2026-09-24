# coder v2 — action-property plan

Source: `.bot/goal-graph-singular/architect/action-property-plan.md` (released 2026-09-24, at 087895908).

Goal: the program holds no Data. An action's property holds its raw value; the run creates the first Data with the run's context.

## Shapes (sent to architect for Ingi's yes before code)
1. `goal/step/action/property/this.cs` gains `Value` (raw item), `Properties` (bag), `Frozen` (build froze it as a default). `IsVariable` goes.
2. `property.list`: `new()` program list (reader/builder Add); `new(module, actionName)` catalog list, lazy reflection. `action.Property { get; init; } = new()`; module.Add makes the catalog element with the catalog list.
3. `action[name]` → property (step-set over frozen).
4. Value-slot read moves onto `type.@this.Read(ref reader, ctx)`; bag read onto `Properties`. The property reads its own row; `data.reader.Row` goes.
5. Writer: non-frozen under `"property"`, frozen under `"default"`; old `"parameter"` → named error.
6. Generator: run Data = `new Data(p.Name, p.Value, context) { Properties = p.Properties.Clone() }`; order step → setting → frozen → [Default].

## Split
- A: 1-6 (compile-coupled).
- B: builder hooks (goal.call, variable.set, file.read, http, llm.query, Build/Callee), default pass (GetDefaults goes), graft typing, mock/intercept, debug; tools/decider python + Properties.llm + Properties.goal key rename.
- C: removals (action.Parameter, parameter/list) + the plan's tests.

Each piece: six C# suites by name vs the previous run, commit, report, wait.

## Status
Waiting for the architect/Ingi on the shapes (Frozen name, type.Read placement, Value).
