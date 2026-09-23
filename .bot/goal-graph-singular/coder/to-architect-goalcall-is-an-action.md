# to-architect — goal.call is an action; GoalCall dies (Ingi's decisions, 2026-09-23)

Answers your §GATE CLOSED items A, B, C. Discussed with Ingi; the decisions below are HIS. Plan and
order are mine — please check them against the July goalcall-one-structure ruling, which this
supersedes and goes further than.

## Decisions (Ingi)

**A. goal.call follows the same pattern as every other type — that is rule #1.**
- The `goal.call` ACTION's own properties are `Name` (text) and `Parameter` (`list<data>`) —
  plain properties on `module/action/goal/call.cs`, read by the standard readers. `list<data>`,
  not `dict`: each argument must carry its own declared type — the same `{name, type, value}` row
  an action parameter is.
- Callback slots hold a **`goal.call` action**, not a `GoalCall` value.
- **`GoalCall` is deleted.**

The `.pr` for `call EmitBuildEvent kind="build-path", path=%path%`:

```json
{"module":"goal","name":"call","parameter":[
  {"name":"Name","type":{"name":"text"},"value":"EmitBuildEvent"},
  {"name":"Parameter","type":{"name":"list"},"value":[
     {"name":"kind","type":{"name":"text"},"value":"build-path"},
     {"name":"path","type":{"name":"text"},"value":"%path%"}]}]}
```

Your fork ("catalog type at read vs type on every answer row") is not needed for goal.call any more:
there is no structured goal.call value left to type — `Name` and `Parameter` are ordinary slots.
The general question (an untyped answer row takes its declared type at the graft) still stands for
other typed slots — say if you want it pursued separately.

Side benefit, measured today: the stage-3 model's most frequent error was putting the call's
arguments BESIDE `GoalName` instead of inside it. Under this shape, beside is correct.

**B. Keep the error model** (Validate returns `IError?`, causes underneath, nothing stored on the
node). #3 shrinks to: `BuildError` (`this.Schema.cs:40`, by-name reflection for `ValidateBuild`)
dissolves into `action.Validate`, which asks the handler through `IBuildValidatable` and adds its
answer to the causes. `IBuildValidatable`/`ValidateBuild` verb+noun naming: separate small item.

**C. `loop.foreach` `ItemName`/`KeyName` → `Item`/`Key`** ("Name" restates what the variable type
already says). `foreach.notes.md` is wrong and gets rewritten to teach `Item`/`Key`. CLAUDE.md's
mention → a proposal entry.

## Where GoalCall lives today (35 production files, 28 test files)

| where | becomes |
|---|---|
| `goal/call.cs` | `Name` (text) + `Parameter` (`list<data>`) + `PrPath` (build-resolved) + `Actor` |
| 11 callback slots — `llm.query` Tool (list) / OnToolCall / OnValidateResponse / OnStream, `http` request.OnStream / download.OnProgress / upload.OnProgress, `event.on` GoalToCall, `channel.set` Goal, `mock.intercept` Call, `environment.run` GoalName | `data<action>` holding a `goal.call` |
| `Events.cs`, `event/lifecycle/binding`, `http/code/Default.cs`, `llm/code/OpenAi.cs` (tool schema from `Name`/`Parameter`), `test/run`, `test/discover` | run the held action |
| `GetGoalAsync` / `LoadFromFile` (on GoalCall) | owned by the `call` action — it finds its own goal |
| `GoalCall.Action` (lookup anchor) | the action itself |
| `GoalCall.Event` (event context) | the binding that holds the action |
| `GoalCall.Parallel` (tools) | an `llm.query` tool concern or a `call` property — your call |
| build: `ResolveGoalCallPaths`, goal.call name repair, `NormalizeParameterTypes` goal.call typing, `ToGoalCall` | read the action's properties; conversion paths die |
| `GoalCall.cs`, `goal/call/Reader.cs`, type-registry entries, `[PlangType("goal.call")]`, the CLR-name guards | deleted (guards: with verification, per July) |
| `.pr` / `Properties.llm` / `goal/call.notes.md` / `build_pr.py` SHAPES hint | the new shape |

## Proposed order

1. **C** — `Item`/`Key` + note. Small, independent.
2. **B** — `BuildError` into `action.Validate`. Small, independent.
3. **A1** — `goal.call` action gets `Name`/`Parameter`; builder prompts, teaching docs, builder `.pr`
   (via `build_pr.py`) move to the shape. `GoalCall` still exists for the callback slots.
4. **A2** — callback slots hold a `goal.call` action, one module per commit: event.on → http →
   llm.query → channel.set → mock → environment.run.
5. **A3** — delete `GoalCall`, its reader, `Convert`/`FromSlots`, the guards.

## Questions for you

1. `Parallel` — where does it go?
2. A callback that runs a held `goal.call` action: through `app.Run(action, context)` like any
   action — agreed, or does the callback site need something the plain dispatch does not give?
3. `Name` as the property name (beside the action's own `"name":"call"` in the .pr) — Ingi's
   wording; I lean keep. Objection?
4. Order OK? C and B first, or do you want A1 before them?
