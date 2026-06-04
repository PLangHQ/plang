# Builder & Runtime Notes

> Part of the App architecture notes — index in [`good_to_know.md`](good_to_know.md).

## Event Override (skipAction)

`event.skipAction` sets `context.EventOverride` to override an action's result. This override is only consumed by action-level event bindings (`BeforeAction`/`AfterAction`). Step-level and goal-level events must NOT consume it, or the override gets eaten before the action handler can see it.

---

## GoalFirst Retry Behavior

When `ErrorOrder` is `GoalFirst`, the error goal runs first. If the error goal **succeeds**, the runtime considers the error handled and returns immediately — **retries are skipped entirely**. This is by design: the error goal resolved the problem, so there's nothing to retry.

Only if the error goal fails (or is absent) does the runtime proceed to retries. This means `GoalFirst` with both a goal and retries configured will only use the retries as a fallback when the error goal can't handle the problem.

`RetryFirst` (the default) is the opposite order: retries run first, the error goal only runs if every retry still fails. `IgnoreError` is the final fallback in both orderings — applied after retry and goal are both exhausted.

See `PLang/app/module/error/handle.cs` for the implementation.

---

## Error Reporting — When to use what

**Rule: match the error mechanism to the return type.**

| Return type | Error mechanism | Example |
|-------------|----------------|---------|
| `Data` or `Data?` | `Data.FromError(new ServiceError(...))` | `GetChild` depth exceeded → `FromError("NavigationDepthExceeded", 400)` |
| `Task<Data>` | Same — return `Data.FromError(...)` | Handler `Run()` methods |
| Constructor / `void` | `throw` — caller must catch | `Data` constructor, `UnwrapJsonElement` |
| `string`, `Type?`, etc. | Return type's natural "not found" (`null`, unchanged value) | `Clr()` → `null`, `ResolveVariablesInPath` → leave unresolved |

**Why this matters:** `Data` has `Error`, `Success`, `Error.Key`, `Error.StatusCode` built in. Returning `null` from a `Data?` method loses information — the caller can't distinguish "not found" from "depth exceeded" or "permission denied." Use `Data.FromError` so the error travels through the normal pipeline with a clear key and status code.

**When a throw converts to Data.FromError:** A method deep inside a Data-returning boundary may throw freely as long as the boundary's try/catch converts to `Data.FromError`. `Decompress()` is the canonical example — it routes through the `application/plang` serializer (which itself returns `Data`) and wraps `InvalidDataException` / `JsonException` into `Data.FromError`. The throw propagates up to the nearest Data-returning boundary. This is fine — just make sure that boundary exists. (Historical note: an earlier `RehydrateNestedData` walk illustrated the same pattern; it was deleted on `data-serialize-cleanup` when Compress/Decompress flattened — the discipline is unchanged.)

---

## Sub-Step Execution — Condition-Gated Skipping

Indented steps (sub-steps) default to NOT executing. They must be "proven true" by a parent condition step. The mechanism:

1. `condition.if` evaluates its condition.
2. It walks the goal's step list from its own index forward, setting `step.Disabled = !conditionResult` on all steps with deeper indent.
3. `Step.Disabled` is a context-backed property — the value is stored on `Context._data` using a key like `step:{prPath}:{index}:disabled`. This keeps the disabled state per-execution, not on the shared Step object.
4. The step runner skips any step where `Disabled == true`.

**Thread safety:** The disabled state lives on the actor's Context data store, not on the Step object itself. Each execution context has its own copy.

**Nesting:** Works at arbitrary depth. When an inner `if` evaluates false, only its immediate indented children are disabled. The outer condition's children at the parent indent level continue normally.

## Condition Orchestration — if/elseif/else in One Step

When a step contains multiple actions and the first is `condition.if`, the condition module orchestrates all actions in the step as branches:

```
Step: "if %x% > 5 set %b% = 4, else set %b% = 0"
Actions: [condition.if, variable.set, condition.if, variable.set]
         ├─ branch 1: condition.if → variable.set (then)
         └─ branch 2: condition.if → variable.set (else)
```

The `Orchestrate()` method:
1. Groups actions into branches: each branch starts with a `condition.if` action, followed by body actions.
2. The last branch with no condition action is the else branch.
3. Evaluates branches in order. The first branch whose condition is true runs its body actions.
4. Returns the result of the matching branch, or `Data(false)` if no branch matched.

**Guard against recursion:** A step-scoped guard key (`__condition_orchestrating_{hashCode}__`) is stored on `Context._data` (not Variables) to prevent the elseif condition evaluations from re-entering orchestration. Inner goal calls from branches get their own guard keys.

---

## Condition Evaluation — Type Normalization

`DefaultEvaluator.NormalizeTypes` handles the JSON numeric boxing problem for conditions:

1. **Both numeric** → convert to the wider type (`byte → short → int → long → float → double → decimal`)
2. **One string, one numeric** → try parsing the string as a number, then normalize
3. **Unknown numeric type** → falls back to `decimal` (the widest), not `byte`

This prevents `InvalidCastException` when comparing `int` vs `long` (a common JSON deserialization mismatch). The `ContainsElement` helper applies the same normalization per-element for collection `contains`/`in` checks.

---

## Action Modifiers — Fold + Grouping

Error handling, caching, and timeouts are **not step-level properties** — they're per-action modifiers. A modifier is a handler that implements `IModifier` and carries `[Modifier(Order = N)]`.

**Runtime.** `Action.RunAsync` hands its dispatch delegate to `Action.Modifiers.RunAsync(innermost, context)`, which walks the list right-to-left. Each action resolves its own handler via `Action.WrapAround` and wraps the running delegate. First in the list = outermost wrapper.

**Builder.** The default `IBuilder.GoalsSave` (`app/module/builder/code/Default.cs`) calls `step.Actions.GroupModifiers(app.Module)` before serialization. The LLM returns a flat list; grouping attaches every `[Modifier]` action to the nearest preceding executable action and sorts each cluster by `Order`. A leading modifier with no preceding executable is dropped and recorded as `DroppedLeadingModifier` in `step.Warnings` so the builder author notices.

**Ordering today:** `timeout=1` (outermost — caps everything including cache lookup), `cache=2` (skip the rest on a hit), `error=3` (innermost — closest to the action).

**Adding a modifier.** Write a handler with `[Modifier(Order = N)]` and implement `IModifier.Wrap`. Normal module discovery picks it up; the LLM sees it in the action registry like any other action.

See `PLang/app/module/IModifier.cs`, `PLang/app/goal/goal/steps/step/actions/action/modifiers/this.cs`, and `PLang/app/goal/goal/steps/step/actions/this.cs` (`GroupModifiers`).

---

## GoalCall — Clone, Never Mutate

Deserialized `GoalCall` instances are **shared**. They come off the `.pr` file and back every invocation of the same step. If two invocations run concurrently (events, future async.fire, HTTP-driven requests), mutating shared `GoalCall` properties (`Parameters`, `Action`) races — one invocation reads the other's `%!error%`.

**Rule:** inside any handler that needs to modify a `GoalCall` before passing it to `RunGoalAsync`, **clone** rather than mutate. Example from `error/handle.cs:CallErrorGoal`:

```csharp
var call = new GoalCall
{
    Name = goalCall.Name,
    Description = goalCall.Description,
    Parallel = goalCall.Parallel,
    Parameters = parameters,
    PrPath = goalCall.PrPath,
    Action = context.Step?.Actions.FirstOrDefault() ?? goalCall.Action
};
return await context.App!.RunGoalAsync(call, context);
```

This pattern applies to any future modifier or handler that parameterises a goal call. Related Clone-family rule: when you add a property to `GoalCall`, update every constructor/clone path that copies it.

---

## Modifier Hardening Backlog

Three accepted-but-unresolved items from security v1 on the modifier feature. Not bugs today — tripwires once new capabilities land.

1. **Negative Ms.** `timeout.after.Ms` and `timer.sleep.Ms` are not validated. `CancelAfter(-2)` and `Task.Delay(-2)` throw `ArgumentOutOfRangeException`. If a developer binds `%ms%` from untrusted external input (HTTP query string, etc.) without sanitising, the modifier throws instead of returning a typed error.
2. **Unbounded RetryCount.** `error.handle.RetryCount` is applied as-is. A `%retryCount%` from untrusted input set to `int.MaxValue` makes the action effectively hang. The inner `Task.Delay` honours cancellation, but a retry with `delayMs == 0` does unbounded work per iteration.
3. **Non-thread-safe cancellation stack.** `Context._cancellationStack` is `Stack<CancellationTokenSource>`. Safe today because handlers execute serially per context, but the roadmap's `async.fire` / `parallel.set` modifiers would run on the same context concurrently. Swap to `ConcurrentStack<T>` or `AsyncLocal<ImmutableStack<T>>` before landing those.

---

## Per-action LLM teaching lives in markdown, not attributes

Action **shape** (what parameters exist, what types, what defaults, is-it-a-modifier) is declared in C# attributes on the handler class — that has to be reflection-readable at compile time. Action **prose** (Description, Notes, Examples) is declared in markdown files at `os/system/modules/<module>/{module,<action>}.{description,notes,examples}.md` and read at catalog-build time by `app.module.MarkdownTeaching.Load(...)`.

`[Description]`, `[ModuleDescription]`, and `[Example]` no longer exist on action handlers. Don't add them back; the rename from "attribute prose" to "markdown prose" was a deliberate move (branch `compile-llm-notes-per-action`) for three reasons:

- **Tuning teaching doesn't rebuild C#.** Edit the `.md`, run the next build, the LLM sees the new prose.
- **Per-action Notes ship scoped, not global.** Notes for one action render in the user message of the Compile call *only when the planner picked that action*. The system prompt keeps just the cross-cutting kernel (modifier-vs-peer classification, formal-mirroring rule, `%!data%`-never-as-fallback). Rules about one action belong with that action.
- **Two-layer merge kills the drift cycle.** `module.<file>.md` is module-wide; `<action>.<file>.md` is action-specific. Renderer concats module-first + blank + action — no override semantics, so a family rule lives **once** at the module layer.

`module` is a reserved stem inside a module folder; no action may be named `module`. Orphan markdown files (stem is neither `module` nor a registered action) are surfaced via `MarkdownTeaching.ScanOrphans` as warnings on the developer's `Output` channel — loud, not fatal.

Renamed attribute: **`[Provider]` → `[Code]`** across the source generator, the attribute definition, every call site, and the PLNG001 diagnostic text. Mechanical, no behaviour change.

Full guide: [`action-catalog.md`](action-catalog.md). Loader source: `PLang/app/module/MarkdownTeaching.cs`. Architect plan: `.bot/compile-llm-notes-per-action/architect/plan.md`.

## Build()-time type stamping — `IClass.Build()`, `(type)` hints, and `BuildWarning`

The companion to *Action `Run()` returns are typed* (above) is the **build-time** side: how the type that rides on a step's terminal `variable.set` gets there in the first place.

Three sources, layered by precedence (highest wins):

1. **User `(type)` hint** — `write to %x%(json)` in the PLang source. The kernel rule lives in `os/system/builder/llm/Compile.llm` and tells the LLM to stamp `Type="json"` on the terminal `variable.set`. Any explicit `Type` on the variable.set (including literal `"object"`) is treated as a user hint and wins.
2. **`IClass.Build()` inference** — the optional compile-time hook on every action handler. Default impl returns `Data.Ok()` (no stamp). A handler that knows enough to infer overrides:
   - `file.read.Build()` — literal `Path` → infer from `path.Extension` via `Formats.Mime`.
   - `llm.query.Build()` — `Schema` set → `Ok("json")`; `Format` set → `Ok(Format)`.
   - `http.request.Build()` / `http.upload.Build()` — literal URL with a recognised extension → infer (query/fragment stripped first, registered-types gated).
3. **LLM-emitted `Type`** — what the planner wrote into the step's terminal `variable.set` based on the action's typed `Run()` return (see the *typed Run()* section).

The validate pass (`builder.code.Default.Validate`) iterates every step, calls `IClass.SetAction(action, context)` to prime the handler's lazy property getters, invokes `Build()`, and:

- `Data.Ok(typeName)` → stamps `typeName` onto the terminal `variable.set`'s `Type` parameter (only if the user didn't already set one — precedence #1 above wins).
- `Data.Ok()` (no value) → no terminal change; LLM-emitted Type stays.
- `Data.Fail(err)` → validate aggregates and fails the build.

`SetAction` is **source-generator-emitted** on each handler partial — it mirrors `ExecuteAsync`'s setup minus the runtime-only steps. Callers (validate) invoke through the `IClass` surface without reflection.

### `BuildWarning` — out-of-band advisory writes

In-band errors stay on `Data` (caller short-circuits, must be in the return path). **Advisory** warnings — "I inferred a type but the literal file you named doesn't exist" — travel through a channel-write instead of bending `Data`'s shape:

```csharp
var ch = Context.App.Channels.Channel("builder");
await ch.WriteAsync(new app.module.builder.warning.@this(this, $"missing literal file: {path}"));
return data.@this.Ok(inferredType);
```

`Channels.Channel(name)` returns a registered channel or a **no-op fallback** (`channel.noop.@this`) — so the handler writes opportunistically without null-checking. (Distinct from `Channels.Resolve(name)`, which returns null on miss and surfaces `ChannelNotFound`.) `Build()` runs in two contexts: under the builder (channel registered, warning surfaces in trace + `--strict`) and outside it (channel absent, the no-op swallows the write).

The warning record `app/module/builder/warning/this.cs` carries `(IClass Action, string Message)` — the writing handler puts `this` in `Action` so the consumer has source attribution without channel-side caller-tagging magic.

Full implementation: `PLang/app/module/builder/code/Default.cs` (the validate-pass + `StampOnTerminalVariableSet` helper).
