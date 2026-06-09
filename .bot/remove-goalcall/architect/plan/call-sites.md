# Call-site disposition — the `GoalCall` leaf-trace

The incumbent is `app.goal.GoalCall` (`PLang/app/goal/GoalCall.cs`). It plays four roles: a **stored reference** (`Name`, `PrPath`), a **runtime argument carrier** (`Parameters`), an **event-binding** (`IEvent`/`Event`), and a **tool flag** (`Parallel`). It also **owns resolution** (`GetGoalAsync` + private `LoadFromFile`). Every role gets a new home below. Line numbers are against `origin/runtime2` at branch creation; treat as anchors, re-grep before editing.

## A. The type and its methods

| Site | Today | Disposition |
|------|-------|-------------|
| `goal/GoalCall.cs` (whole file) | the type | **Delete.** Resolution → `app.Goal.Load`; conversion → Goal's hook; the rest evaporates. |
| `goal/GoalCall.cs:147` `GetGoalAsync` | four-tier resolve | **Move** to `app.Goal.Load(name, context, prPath?)`. Uses `context.Goal` as the caller anchor (set in `Goal.RunAsync`), not an `Action` field. |
| `goal/GoalCall.cs:255` `LoadFromFile` (private) | read `.pr`, wire back-refs, match sub-goal | **Fold** into `app.Goal.Load`'s file path. Back-ref wiring (`Goal.App`, `Step.Goal`, `subGoal.Parent`, `LoadedFromPrPath`) is Goal's own concern — keep it together. |
| `goal/GoalCall.cs:44` `Convert` | string/JsonElement/dict → goal.call value | **Move** to Goal's value-conversion hook → calls `app.Goal.Load`. Keep the CLR-type-name guard. |
| `this.cs:538` `RunGoalAsync(GoalCall)` | resolve + inject params + run | **Delete.** Its three jobs split: resolve → `Load`, inject → the `goal.call` handler, run → `app.Run`. |
| `this.cs:563` `RunGoalAsync(Goal)` | run a resolved goal | **Rename** → `app.Run(goal, context)`. The bare runner, one job. |
| `this.cs:522` Start `new GoalCall{PrPath=…}` + `GetGoalAsync` | bootstrap entry resolve | **Reroute.** Start keeps actor selection, then `app.RunAction(new goal.Call{ Goal: Load(entry), Actor: User }, ctx)`. No GoalCall. |

## B. Parameters to retype: `data.@this<GoalCall>` → `data.@this<Goal>`

| Site | Property | Note |
|------|----------|------|
| `module/goal/call.cs:14` | `GoalName` | **Split** → `Goal` (`data.@this<Goal>`) + `Parameters` (`List<data>`) + hidden `PrPath`. |
| `module/event/on.cs:20` | `GoalToCall` | → `data.@this<Goal>`. Resolve at registration (context.Goal = declaring goal). Dynamic handler names: capture declaring folder (a `path`), not an `Action`. |
| `module/http/request.cs:52` | `OnStream` | → `data.@this<Goal>`. Callback args injected at fire time by the caller. |
| `module/http/upload.cs:50` | `OnProgress` | → `data.@this<Goal>`. |
| `module/http/download.cs:37` | `OnProgress` | → `data.@this<Goal>`. |
| `module/mock/intercept.cs:14` | `Call` | → `data.@this<Goal>`. |
| `module/environment/run.cs:13` | `GoalName` | → `data.@this<Goal>`. (`run.cs:22` calls `RunGoalAsync(GoalName.Value, …)` → goal.call.) |
| `module/channel/set.cs:20` | `Goal` | → `data.@this<Goal>`. (`set.cs:44` `GetGoalAsync` → `Load`.) |
| `module/llm/query.cs:43` | `Tools` (`List<GoalCall>`) | → `List<Goal>`. **Coordinate with tools-as-actions branch** — `Parallel` becomes a described action property. |
| `module/llm/query.cs:47,51,55` | `OnToolCall`, `OnValidateResponse`, `OnStream` | → `data.@this<Goal>`. |

## C. Internal callers — reroute through `goal.call` (or `app.Run` for already-resolved goals)

| Site | Today | Disposition |
|------|-------|-------------|
| `module/builder/this.cs:141` | `new GoalCall{Name="Build",…}` + `RunGoalAsync` | `goal.call(Goal: Build, Actor: User)`. |
| `module/builder/code/Default.cs:1130` | `GetGoalAsync` (build-time validate) | → `app.Goal.Load`. This is the `Build()` validation path. |
| `module/test/run.cs:206` | `new GoalCall{PrPath=…}` + `RunGoalAsync` | `goal.call`. |
| `module/test/discover.cs` | references | re-grep; likely same pattern. |
| `module/llm/code/OpenAi.cs:424,540,566,590` | validation/start/exec/end calls | each → `goal.call` with the tool args as `Parameters`. |
| `module/llm/ToolCall.cs` | references | re-grep; tool-execution shape. |
| `module/http/code/Default.cs:780` | `RunGoalAsync(call)` | callback → `goal.call`, runtime data as `Parameters`. |
| `module/ui/code/Fluid.cs:227` | `RunGoalAsync(goalCall)` | `callGoal` Fluid tag → `goal.call`. |
| `channel/type/goal/this.cs:76` | `RunGoalAsync(Goal)` | already a resolved Goal → `app.Run(goal)`. |
| `goal/setup/this.cs:97` | `RunGoalAsync(goal)` | resolved Goal → `app.Run(goal)`. |
| `module/error/handle.cs:165` | stamps step as nav anchor | anchor is now `context.Goal`; verify nested `goal.call` inside `error.handle.Actions` still resolves siblings. |

## D. Events plumbing

| Site | Today | Disposition |
|------|-------|-------------|
| `module/Events.cs:18,19` | `Before`/`After` → `List<GoalCall>` + `Stamp` | Bindings hold resolved `Goal`s; drop the `Event`/`Action` stamping. |
| `module/Events.cs:41` `Stamp` | sets `gc.Action` + `gc.Event` | **Delete** the event-stamping. (Action-anchor no longer needed — resolution uses `context.Goal`.) |
| `actor/context/this.cs:439` `GetEventBindings` | → `List<GoalCall>` | → `List<Goal>` (or the binding type). |
| `event/lifecycle/binding/this.cs:31` | `GoalToCall: GoalCall?` | → `Goal?` (resolved at registration). |
| `module/IEvent.cs` | `IEvent` + `EventContext` | **Delete `IEvent`.** **Keep `EventContext`** (`%!event%` reads it) — now set by the firing path (`binding.Run`) into `context.Event`, around the handler, via the `context/this.cs:289` scope. |
| `actor/context/this.cs:123,181,289` | `context.Event`, `%!event%`, save/restore scope | **Keep.** This is where `%!event%` now gets set from. |

## E. Type-system / serialization / catalog references

These reference `GoalCall` as a registered `[PlangType("goal.call")]` or as `path.GoalCall`. Re-grep each; most are catalog wiring that follows from the conversion-hook move.

| Site | Note |
|------|------|
| `app/GlobalUsings.cs:3` `global using GoalCall = …` | **Delete.** |
| `type/path/this.cs:144` `path.GoalCall` property | **Delete** — a `path` becomes a `Goal` via `app.Goal.Load`, not a `GoalCall`. (Check the `Start`/entry path that used it.) |
| `type/catalog/Conversion.cs:400` (+ hook dispatch) | Re-point the `goal.call` conversion to Goal's hook. |
| `type/catalog/Registry.cs`, `goal/serializer/Default.cs`, `Diagnostics/Format.cs`, `module/Attributes.cs`, `Attributes/PlangTypeAttribute.cs`, `data/this.cs`, `type/path/file/this.Operations.cs`, `channel/serializer/list/this.cs`, `channel/serializer/filter/Tagged.cs` | Catalog/serializer mentions of the `goal.call` type. Audit during Stage 4; the PLang type name `goal.call` may stay as the wire identity of the `goal.call` *action*, but the C# `GoalCall` type and its `[PlangType]` registration go. |

## Things to pin with tests

- Callee AST never embeds in caller `.pr` — only the name (+ hidden prPath) is stored.
- `Goal.Value` resolves typed error (`GoalNotFound`/`InvalidPrFile`) when missing, not an exception.
- `Parameters` reach the callee as typed/signed `Data` (not stringified) and land in the target actor's context when `Actor` is set.
- `CurrentActor` switches to the target during a cross-actor `goal.call` and restores after; User's event bindings fire (not System's) during the entry goal.
- `%!event%` still readable inside an event-handler goal after `IEvent` deletion.
- `Build()` warns on an unknown static goal name, matching today.
- Cycle detection (self-call, mutual recursion) still trips at the callstack `Push`.
