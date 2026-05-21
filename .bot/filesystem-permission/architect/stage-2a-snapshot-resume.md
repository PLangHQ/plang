# Stage 2a: Snapshot-resume

**Goal:** One mechanism for suspend/resume — `Snapshot`. An action attaches a `Snapshot` to its return Data when it wants to exit the goal; the step loop short-circuits; the wire carries the Snapshot; resume restores the chain and re-enters via a recursive walk. The two existing callback classes (`AskCallback`, `ErrorCallback`) are scaffolding — zero production callers — and get deleted.

Stage 2b's `Path.Authorize` rides on top: calls `output.ask`, gets either a synchronous answer (stateful channel) or an Exit-typed Data (stateless). No permission-specific machinery in stage 2a.

## Out of scope

- New channels. Stream channel is the only one whose ask-blocking path is wired today; that's what stage 2a exercises. Message/HTTP channel's `Ask` body lands when HTTP work ships.
- Permission semantics — stage 2b.

## Flow

`- read /path/to/file.txt` mid-goal, no grant, against a stateless channel:

```
file.read action
  ↓ Authorize(verb=Read)                  [stage 2b helper]
  ↓ output.ask("Allow…?")
  ↓ Channel.Ask on Message: builds Data<Ask> with Snapshot attached
  ↓ Authorize sees Type.Exit() == true → returns it
  ↓ file.read returns it

Step loop: result.ShouldExit() → short-circuit, return result.

Channel layer:
  serialize { value:question, snapshot, signature } → wire → response.
  process can go idle.

         ━━━━━━━━━━━━━━━━━━━━━━━━

Next request: { answer:"a", snapshot, signature }:
  channel deserializes → produces verified Data (Data == verified by construction).
  channel sets Context.Variables["!ask.answer"] = "a".
  channel invokes callback.run with the Data.

callback.run → Data.Snapshot.Resume(ctx)
  → Restore(snapshot, ctx) populates RestoredChain
  → ResumeChain walks outer→inner, pushes parent frames, recurses
  → bottom: Goal.RunFrom(stepIdx, actionIdx) — re-runs suspended action
  → output.ask sees !ask.answer → returns Data.Ok("a")
  → Authorize signs grant + stores → returns Ok
  → file.read reads bytes → step continues → goal continues
  → on unwind, each parent goal continues from action after its `call SubGoal`
```

Stateful (Stream) channel: same path until `Channel.Ask`, which blocks on stdin and returns `Data.Ok(line)` directly. No Snapshot, no short-circuit, no resume. Everything synchronous in-process.

## Deliverables

### 1. `IExitsGoal` marker + `Type.Exit()` extension

`IExitsGoal` is an empty marker interface in `App` namespace. Types whose presence in a step result means "stop here" implement it. Only one implementer in stage 2a: `Ask`.

`Type.Exit()` is a pure-verb extension on `System.Type`:

```csharp
public static class TypeExitExtensions
{
    public static bool Exit(this System.Type clrType)
        => typeof(global::App.IExitsGoal).IsAssignableFrom(clrType);
}
```

The engine queries via `result.Type?.ClrType?.Exit()` — never decomposes the Data's Value.

### 2. `Ask` — empty marker class

Lives at `PLang/App/modules/output/ask.cs` (lowercase filename, colocated with the action partial class):

```csharp
public sealed class Ask : global::App.IExitsGoal { }
```

The question text rides as `Data.Value`. JSON wire shape is single-layer:

```json
{ "type": "ask", "value": "Allow user to read /apps/Email/system.sqlite?",
  "signature": {...}, "snapshot": {...} }
```

Strong typing via the generic: `Data<Ask>` says "this is an Ask," `Type.Exit()` is true because `typeof(Ask)` implements `IExitsGoal`.

### 3. `Data.Snapshot` field + action.Snapshot() helper

`Data.@this` gains:

```csharp
public Snapshot.@this? Snapshot { get; set; }
```

`Action.@this` (the partial class actions inherit) gains a `Snapshot()` helper. Captures App state from the action's perspective — call it *inside the action handler*, while the Call frame is still alive:

```csharp
public partial class @this
{
    public Snapshot.@this Snapshot() => Context.App.Snapshot();
}
```

(The `App.Snapshot()` factory already walks subsystems and returns the full tree, `PLang/App/this.Snapshot.cs:16-27`. The OBP-clean home is `Snapshot.@this.Capture(ctx)` as a static factory — tracked in `todos.md` as follow-up cleanup. Stage 2a uses the existing entry as-is.)

**Contract:** any action whose result Type satisfies `Exit()` MUST have called `action.Snapshot()` and attached the result to its Data. Test asserts.

### 4. Action owns its execution — drop `App.Run` / `App.RunAction`

Today `App.Run` is the shared entry for both step-loop dispatch (`Action.RunAsync:164` delegates here) and C# helper invocations. Two concerns sharing machinery. **Action knows how to run itself.** Collapse:

**a. `Action.@this.Synthetic` property.** Defaults to `true` (the common case for inline C# construction). Source generator emits `Synthetic = false` for PR-built actions.

```csharp
public partial class @this
{
    public bool Synthetic { get; init; } = true;
}
```

**b. `Action.RunAsync(context)` is the single entry.** Absorbs today's `App.Run` body (Push/Anchor/Execute) into the partial class. Lifecycle and Modifiers stay where they already are, around the dispatch.

**c. `CallStack.Push` reads `action.Synthetic`** and stamps the Call frame. No new parameter.

**d. `App.Run`, `App.RunAction`, and the `cause` parameter all delete.** Every C# call site updates:

```csharp
// Before                                  // After
await Context.App.RunAction<T>(a, ctx);   await a.RunAsync(ctx);
```

Survey: ~20-30 call sites across signing, crypto, builder, GoalCall, Data.Envelope, and the to-be-deleted callback classes. Mechanical.

**e. Snapshot capture filters by `action.Synthetic` at wire-serialize time.** In-memory full Snapshot keeps synthetic frames (debug, telemetry). The serializer drops them from the wire shape — synthetic frames can't be restored from PR and get recreated naturally by the resumed execution.

### 5. Engine: step-loop short-circuit + `Data.ShouldExit`

`Steps.RunAsync` (today: `Goals/Goal/Steps/this.cs:154`) gains one branch. Wrap the three distinct stop conditions in a `Data.ShouldExit` extension:

```csharp
public static class DataShouldExitExtensions
{
    public static bool ShouldExit(this Data.@this d) =>
        (!d.Success && !d.Handled)
        || d.Returned
        || (d.Type?.ClrType?.Exit() == true);
}
```

Step loop, `Step.RunAsync`, and `Goal.RunFrom` all use:

```csharp
if (result.ShouldExit()) return result;
```

The three flags stay distinct (consumers differentiate downstream); only the loop-stop check unifies.

### 6. `output.ask` → `Channel.Ask`

`output.ask` (`modules/output/ask.cs`) keeps its resume-consume sentinel and delegates to the channel. ~10 lines:

```csharp
public async Task<Data.@this> Run()
{
    var answer = Context.Variables.Get(AnswerVariableName);
    if (answer != null && answer.IsInitialized)
    {
        Context.Variables.Remove(AnswerVariableName);
        return Data.@this.Ok(answer.Value);
    }
    return await Context.Actor.Channels.Input.Ask(this);
}
```

`Channel.Ask` takes the action directly (no `IAskRequest` interface — channel can know about `output.ask`):

```csharp
// PLang/App/Channels/Channel/this.cs
public abstract Task<Data.@this> Ask(modules.output.ask action, CancellationToken ct = default);
```

(Existing `AskCore` renamed to `Ask`. `WriteCore` similarly renamed to `Write` and takes the action directly — extracts `Question.Value` itself instead of having the caller pre-wrap a Data prompt.)

**Stream channel `Ask`** — blocks on stdin:

```csharp
public override async Task<Data.@this> Ask(modules.output.ask action, CancellationToken ct = default)
{
    var writeRes = await Write(action, ct);
    if (!writeRes.Success) return writeRes;
    // existing read-line + timeout logic
    return Data.@this.Ok(line ?? string.Empty);
}
```

**Message channel `Ask`** — produces the Exit-typed Data with Snapshot:

```csharp
public override Task<Data.@this> Ask(modules.output.ask action, CancellationToken ct = default)
{
    var data = new Data.@this<Ask>("", action.Question.Value);
    data.Context  = action.Context;
    data.Snapshot = action.Snapshot();
    return Task.FromResult<Data.@this>(data);
}
```

### 7. `Goal.RunFrom(ctx, stepIdx, actionIdx)`

Continuation helper. Runs the action at `(stepIdx, actionIdx)`, then any remaining actions in that step, then remaining steps in the goal:

```csharp
public partial class @this
{
    public async Task<Data.@this> RunFrom(Actor.Context.@this ctx, int stepIdx, int actionIdx)
    {
        var step = Steps[stepIdx];
        var result = await step.RunFrom(ctx, actionIdx);
        if (result.ShouldExit()) return result;
        return await Steps.RunAsync(ctx, fromIndex: stepIdx + 1);
    }
}
```

Adds `Step.RunFrom(ctx, fromActionIdx)` next to existing `Step.RunAsync`, and an overload `Steps.RunAsync(ctx, fromIndex: int)`. Used only by `ResumeChain` (#8) and Authorize-resume — not exposed beyond the runtime layer.

### 8. `Snapshot.Resume(ctx)` — recursive cross-goal continuation

Snapshot owns its own resume, paired with `App.Snapshot()` capture. Walks the captured chain recursively so each goal in the chain re-enters at its right position:

```csharp
public async Task<Data.@this> Resume(Actor.Context.@this context)
{
    Restore(this, context);                              // populates RestoredChain
    var chain = context.App.CallStack.RestoredChain;
    if (chain == null || chain.Count == 0)
        return Data.@this.FromError(new ServiceError(
            "Resume has no frames after Restore", "NoPosition", 400));
    return await ResumeChain(chain, 0, context);
}

private static async Task<Data.@this> ResumeChain(
    IReadOnlyList<Call.Position> chain, int idx, Actor.Context.@this ctx)
{
    var frame = chain[idx];

    // Bottom: re-enter the goal at the suspended (StepIndex, ActionIndex).
    if (idx == chain.Count - 1)
        return await frame.Goal.RunFrom(ctx, frame.StepIndex, frame.ActionIndex);

    // Parent: its action is a "call SubGoal" mid-flight. Push so children see
    // it as caller, recurse into the sub-goal, then continue from ActionIndex+1.
    await using var callFrame = ctx.App.CallStack.Push(frame.Action, ctx.Variables);

    var subResult = await ResumeChain(chain, idx + 1, ctx);
    if (subResult.ShouldExit()) return subResult;

    return await frame.Goal.RunFrom(ctx, frame.StepIndex, frame.ActionIndex + 1);
}
```

The recursion handles nested goals: when the suspended sub-goal completes, the parent continues from the action *after* the `call SubGoal` action. Multi-action steps like `- call X, write to %y%` work naturally because `ActionIndex + 1` may land on the next action in the same step.

`ResumeChain` is acknowledged-clunky and tracked in `todos.md` for revisit during implementation; works as designed.

### 9. `callback.run` rewrite

The resume entry shrinks. Data is verified by construction (wire deserializer enforces it before producing a Data at all), so no explicit verify call:

```csharp
[Action("run", Cacheable = false)]
public partial class run : IContext
{
    [IsNotNull]
    public partial Data.@this Data { get; init; }

    public Task<Data.@this> Run()
    {
        if (Data.Snapshot == null)
            return Task.FromResult(Data.@this.FromError(new ServiceError(
                "Resume invoked on Data without a Snapshot", "NoSnapshot", 400)));
        return Data.Snapshot.Resume(Context);
    }
}
```

The answer flows via `Context.Variables` (channel sets `!ask.answer` before invoking this action — sentinel naming flagged for future cleanup in `todos.md`).

### 10. Drop dead code

After 1–9 land and tests pass:

- Delete `PLang/App/Callback/ICallback.cs`.
- Delete `PLang/App/Callback/AskCallback.cs`.
- Delete `PLang/App/Callback/ErrorCallback.cs`.
- Delete `PLang/App/Callback/Wire/` (per-callback wire shapes fold into Snapshot's serialization).
- Update `PLang/App/Errors/Error.cs:55` — `Callback` property becomes `Data<Snapshot>`.
- Adapt tests under `PLang.Tests/App/CallbackTests/` — round-trip tests rewrite to exercise Snapshot serialization + `Snapshot.Resume`; per-callback Deserialize tests delete.
- Confirm `App.Run`, `App.RunAction`, and the `cause` parameter are gone from the codebase.

## Tests (integration)

- **Stream / stateful, mid-goal:** `- ask user "name?", write to %name%` / `- write out %name%` against a Stream channel piped with `"Alice"` on stdin. Goal completes; `%name% == "Alice"`; no Snapshot captured.
- **Message / stateless, mid-goal:** same goal against a fake Message-like channel. Step 1 returns `Data<Ask>` with Snapshot. Goal short-circuits. Invoke `callback.run` with `{ snapshot, !ask.answer="Alice" }`. After resume, `%name% == "Alice"`; step 2 ran.
- **Cross-goal continuation (nested suspend/resume):**
  ```
  Start
  - write out "Hello"
  - call AskAQuestion
  - write out "%answer%"

  AskAQuestion
  - write out "Asking"
  - ask user "name?", write to %answer%
  ```
  Stateless channel; suspend fires inside `AskAQuestion`. Snapshot captures `[Start#callStep, AskAQuestion#askStep]`. After resume with `"Alice"`: AskAQuestion finishes → unwind to Start → continue after `call AskAQuestion` → `write out "%answer%"` runs. Final output across capture+resume: `"Hello\nAsking\nAlice"`.

Unit tests cover each deliverable individually (`Type.Exit()` predicate, `Synthetic` default and source-gen override, `ShouldExit` flag combinations, etc.).

## Dependencies

- Existing `App.Snapshot.@this`, `App.CallStack.this.Snapshot.cs` (Capture + Restore — unchanged).
- Existing `Channels.Channel.Stream.@this.AskCore` body (renamed and signature-updated; logic preserved).
- No new packages.

## Acceptance

- `Snapshot.Resume(ctx)` is the only path the channel/host uses to reconstitute a suspended goal. No `ICallback.Run` dispatch anywhere in the codebase.
- `Type.Exit()` is the only engine-side discriminator for "this Data exits the goal."
- `output.ask` is ~10 lines (resume-consume sentinel + delegate to `Channel.Ask`).
- `App.Run` and `App.RunAction` symbols absent from the codebase.
- Mid-goal `output.ask` works on Stream (synchronous) and on Message (suspend/resume round-trip).
- Cross-goal continuation works end-to-end (the nested integration test passes).
- `dotnet run --project PLang.Tests` zero regressions. `Tests/Callback/AskWithVars` and `AskVarsResumeBindsValue` adapted to the new shape.

## Snapshot wire reference

`App.Snapshot()` walks every `ISnapshot` subsystem: Variables, Errors, Providers, Statics, Build, Testing, CallStack.

**Full snapshot** of a paused mid-goal action:

```json
{
  "Variables": { "entries": [ { "name": "%userId%", "value": "u-42", "type": "string" } ] },
  "CallStack": {
    "frames": [
      { "goalPrPath": "/app/start.pr", "goalHash": "abc123",
        "stepIndex": 0, "actionIndex": 0, "id": "f1",
        "variables": { /* per-call diffs */ } }
    ]
  },
  "Errors":    { "trail": [...] },
  "Providers": { ... },
  "Statics":   { ... },
  "Build":     { ... },
  "Testing":   { ... }
}
```

**Stateless ask-resume wire** — just CallStack + Variables:

```json
{
  "Variables": { "entries": [ { "name": "%userId%", "value": "u-42", "type": "string" } ] },
  "CallStack": {
    "frames": [
      { "goalPrPath": "/app/start.pr", "goalHash": "abc123",
        "stepIndex": 0, "actionIndex": 0, "id": "f1" }
    ]
  }
}
```

**Error-resume wire** — adds `Errors` for failure context.

Per-channel serializers own section-filtering. Stateful channels never serialize a Snapshot (in-process). Stateless channels declare a serializer that knows what to include for their resume kind (ask vs error vs ...). Filtering strategy (allowlist on one serializer, distinct serializers per kind, or per-subsystem hints) is the coder's call — tracked in `todos.md`.

## What this stage unblocks

- **Stage 2b** — has working `output.ask` + Snapshot + resume to ride on.
- **Mid-goal `output.ask`** — works end-to-end for the first time, stateful and stateless.
- **Any future Exit-typed kind** — implements `IExitsGoal`, ships through the same machinery. No per-kind callback classes.
