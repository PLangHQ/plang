---
name: Stage 2a — Snapshot-resume (unify suspend/resume; drop callback classes)
description: One mechanism for suspend/resume — Snapshot. ICallback / AskCallback / ErrorCallback dissolve; Ask is just a Type the engine recognises.
type: stage
---

# Stage 2a: Snapshot-resume

**Goal:** Unify all suspend/resume flows through `Snapshot`. Today the codebase has two parallel callback classes (variable-bind / app-restore-dispatch) — both scaffolding only, zero production callers outside the Wire serializer comment. Replace both with a single rule: when an action returns a Data whose Type "exits the goal", the action attaches a `Snapshot` to that Data; the step loop short-circuits the goal. Whoever holds the result (channel, in-process resumer, wire receiver) restores via `App.Restore(snapshot)` and dispatches from the captured Position. The two existing callback classes are deleted.

Stage 2b's `Path.Authorize` rides on top — calls `output.ask`, gets either a synchronous answer (stateful channel) or an Exit-typed Data (stateless channel). No permission-specific machinery.

## What this stage is NOT

- **Not a new channel.** Stream channel is the only one whose ask-blocking path is fully wired today; that path is what stage 2a exercises end-to-end. The Message/HTTP channel's `Ask` body lands when HTTP work happens — parked.
- **Not permission-specific.** `Permission`, `Path.Authorize`, all permission semantics belong to stage 2b.
- **Not a new abstraction.** `Snapshot` already exists (`PLang/App/Snapshot/this.cs`, `PLang/App/CallStack/this.Snapshot.cs`). Stage 2a reuses it as the only suspend/resume currency.

## Flow

`- read /path/to/file.txt` mid-goal, no grant, against a stateless channel:

```
file.read action
  ↓ Authorize(verb=Read)                          [stage 2b helper]
  ↓ no grant; output.ask(question="Allow…?")
  ↓ Channel.Ask on Message channel: returns Data<Ask>(question) with Snapshot attached
  ↓ Authorize sees result.Type.Exit() == true → returns it
  ↓ file.read returns it

Step loop sees result.Snapshot != null (the action attached it):
    return result                                  ← short-circuits the goal

Channel layer holds the result:
    serialize { question, snapshot } to wire, sign, write response
    process can now go idle / die

         ━━━━━━━━━━━━━━━━━━━━━━━━

Next request: { answer:"a", snapshot }:
    verify signature
    App.Restore(snapshot, ctx)
    ctx.Variables.Set("!ask.answer", "a")
    App.Run(snapshot.BottomFrame.Action, ctx)      ← re-runs file.read
    Steps.RunAsync(ctx, fromIndex: BottomFrame.StepIndex + 1)

    During the re-run: file.read → Authorize → output.ask sees !ask.answer →
    returns Data.Ok("a") → Authorize signs grant + stores → Ok → file.read
    proceeds with the actual read → next step runs.
```

Against a stateful (Stream) channel the Authorize/output.ask chain runs, but the channel's `Ask` blocks on stdin instead of returning Exit-typed Data. The Snapshot capture / short-circuit path never fires. Goal completes synchronously.

## Deliverables

### 1. `Type.Exit()` extension method on `System.Type`

Lives at `PLang/App/Data/TypeExit.cs` (or wherever Type extensions belong; coder picks). Pure-verb name per OBP — no noun suffix.

```csharp
public static class TypeExitExtensions
{
    public static bool Exit(this System.Type clrType)
        => typeof(global::App.IExitsGoal).IsAssignableFrom(clrType);
}
```

`IExitsGoal` is a single-marker interface in `App` namespace. The few types whose presence in a step result means "stop here and capture state" implement it. `Ask` is the only implementer in stage 2a. Stage 2b's permission deny does **not** use this — denial is a normal `Data.Fail`, propagates the usual way.

**Tests:**
- `typeof(modules.output.Ask).Exit()` → true.
- `typeof(string).Exit()` → false.
- `typeof(byte[]).Exit()` → false.

### 2. `Ask` type — empty marker class, the only Exit-typed kind in stage 2a

Sits at `PLang/App/modules/output/ask.cs` (lowercase filename, alongside the action partial class).

```csharp
public sealed class Ask : global::App.IExitsGoal { }    // empty marker
```

The question text rides as `Data.Value` — no wrapping. JSON wire shape is single-layer:

```json
{ "type": "ask", "value": "Allow user to read /apps/Email/system.sqlite?",
  "signature": {...}, "snapshot": {...} }
```

Strong typing preserved via the generic — `Data<Ask>` says "this is an Ask," and `Type.Exit()` is true because `typeof(Ask)` implements `IExitsGoal`. The previous `AskCallback` fields (Position, Variables, ActorName) all live in `Snapshot` now.

### 3. Action owns the Snapshot; engine short-circuits on `result.Snapshot != null`

`Data.@this` grows one optional field that serialises (it's part of the wire shape — the channel reads it back on resume):

```csharp
public Snapshot.@this? Snapshot { get; set; }
```

**The action owns capture.** An action that wants to exit the goal builds its Snapshot at the action level — *before* returning, while its own Call frame is still alive. This is essential: at step level, the action frame has already popped and we'd lose which action triggered the Exit (a step can have multiple actions).

`Action.@this` (the partial class actions inherit) grows a `Snapshot()` helper. Uses the existing `App.Snapshot()` factory (`PLang/App/this.Snapshot.cs:16-27`) — which already walks subsystems and returns the full tree:

```csharp
// PLang/App/Goals/Goal/Steps/Step/Actions/Action/this.Snapshot.cs (or similar)
public partial class @this
{
    /// Captures App state from the action's perspective. Call from inside an
    /// action handler *before* returning an Exit-typed Data. The captured
    /// CallStack ends at this action's live frame.
    public Snapshot.@this Snapshot() => Context.App.Snapshot();
}
```

**OBP relocation note (tracked in todos.md):** the orchestration that walks subsystems currently lives on `App.Snapshot()`. The OBP-clean home is `Snapshot.@this.Capture(ctx)` as a static factory — App shouldn't need to know how a Snapshot is built. Out of scope for this stage; tracked as a follow-up cleanup. Stage 2a uses the existing entry as-is.

Action handlers that exit attach the snapshot themselves. Single-layer Data construction — the question rides directly as `Value`:

```csharp
// inside Message channel's Ask
public override Task<Data.@this> Ask(modules.output.ask action, CancellationToken ct = default)
{
    var data = new Data.@this<Ask>("", action.Question.Value);   // Value = question string
    data.Context  = action.Context;
    data.Snapshot = action.Snapshot();                            // action owns capture
    return Task.FromResult<Data.@this>(data);
}
```

**Step loop adds one branch.** `Steps.RunAsync` (`Goals/Goal/Steps/this.cs:154`) checks `Type.Exit()` — the primary signal. Snapshot existence is a contract assertion, not the engine's decision criterion (Snapshot may exist on a Data for other reasons later — debug, audit — and we don't want any Snapshot to trigger exit):

```csharp
result = await step.RunAsync(context);

if (!result.Success && !result.Handled) return result;
if (result.Returned) return result;
if (result.Type?.ClrType?.Exit() == true) return result;    // NEW: short-circuit
```

**Contract:** any action whose Type is `Exit()`-true MUST have called `action.Snapshot()` and attached it to its Data. Test asserts (Exit-typed result with `Snapshot == null` is a bug).

**Tests:**
- 3-step goal where step 1 returns `Data<Ask>` — steps 2/3 do NOT execute. Returned Data has `.Snapshot` populated; `.Snapshot`'s CallStack frame chain ends at step 1's action.
- 3-step goal where step 1 returns `Data<string>` — steps 2/3 DO execute (regression guard).
- Action that constructs Exit-typed Data without calling `Snapshot()` — caught by a contract test asserting "every Exit-typed result has `Snapshot != null`."
- Action that calls `Snapshot()` with a non-Exit Value Type — also caught (predicate must be true).

### 4. Route `output.ask` through `Channel.Ask`; remove inline AskCallback construction

Today `output.ask` (`modules/output/ask.cs:37-81`) always builds an `AskCallback` inline, bypassing the channel entirely. After this stage:

```csharp
public async Task<Data.@this> Run()
{
    // Resume — sentinel binding from previous suspend
    var answer = Context.Variables.Get(AnswerVariableName);
    if (answer != null && answer.IsInitialized)
    {
        Context.Variables.Remove(AnswerVariableName);
        return Data.@this.Ok(answer.Value);
    }

    // Delegate. Stream blocks; Message returns Data<Ask>.
    return await Context.Actor.Channels.Input.Ask(this);
}
```

**`Channel.Ask` takes the action directly.** No `IAskRequest` interface. The channel knows about `output.ask` — that's appropriate since "ask" is the channel's reason for exposing an Ask method. Direction of dependency is fine.

```csharp
// PLang/App/Channels/Channel/this.cs
public abstract Task<Data.@this> Ask(modules.output.ask action, CancellationToken ct = default);
```

(Existing `AskCore` name dies. Pure verb on the channel.)

**Stream channel `Ask`** — takes the action, decomposes `Question.Value` itself, writes, blocks on stdin, returns `Data.Ok(line)`:

```csharp
public override async Task<Data.@this> Ask(modules.output.ask action, CancellationToken ct = default)
{
    var writeRes = await Write(action, ct);          // see Write rename below
    if (!writeRes.Success) return writeRes;
    // existing read-line + timeout logic from Stream/this.cs:97-120
    return Data.@this.Ok(line ?? string.Empty);
}
```

(`WriteCore` similarly renamed to `Write` and takes the action directly — extracts `Question.Value` itself rather than the caller pre-wrapping a `Data` prompt. Same OBP smell, same fix.)

**Message channel `Ask`** — returns `Data<Ask>` with a Snapshot captured at action level (per #3). No Position / Variables / Actor here — Snapshot carries it:

```csharp
public override Task<Data.@this> Ask(modules.output.ask action, CancellationToken ct = default)
{
    var ask = new Ask(action.Question.Value);
    var data = new Data.@this<Ask>("", ask);
    data.Context  = action.Context;
    data.Snapshot = action.Snapshot();       // action owns capture (deliverable #3)
    return Task.FromResult<Data.@this>(data);
}
```

`Context.App.CallStack` BottomFrame at `action.Snapshot()` time is `output.ask`'s own frame (it's still live when this code runs from inside the action's Run). The smart "walk-up-to-step-action" logic stays inside `Snapshot` capture if the action is synthetic (no Step) — same as before, just lives in the snapshot path now.

### 5. Single resume entry — `App.Run(Snapshot, ctx)`

Today `modules/callback/run.cs` dispatches polymorphically via `ICallback.Run`. After this stage, the resume action is a thin wrapper around a new `App.Run(Snapshot, ctx)` overload:

```csharp
[Action("run", Cacheable = false)]
public partial class run : IContext
{
    /// <summary>The suspended Data sent back by the channel. Carries Signature + Snapshot.</summary>
    [IsNotNull]
    public partial Data.@this Data { get; init; }

    public async Task<Data.@this> Run()
    {
        var v = await Context.App.RunAction<modules.signing.verify>(
            new modules.signing.verify { Data = Data }, Context);
        if (!v.Success) return v;

        if (Data.Snapshot == null)
            return Data.@this.FromError(new ServiceError(
                "Resume invoked on Data without a Snapshot", "NoSnapshot", 400));

        // One call. App.Run(snapshot) does Restore + run-from-position-forward.
        return await Context.App.Run(Data.Snapshot, Context);
    }
}
```

**`App.Run(Snapshot, ctx)` is the new overload** — sibling of `App.Run(action, ctx)`. Does everything from one entry:

```csharp
public async Task<Data.@this> Run(Snapshot.@this snapshot, Actor.Context.@this context)
{
    Restore(snapshot, context);
    var bottom = context.App.CallStack.BottomFrame;
    if (bottom == null)
        return Data.@this.FromError(new ServiceError(
            "Resume has no bottom frame after Restore", "NoPosition", 400));

    // Run from the suspended action forward — through any remaining actions in
    // the same step, then through subsequent steps in the goal. (Deliverable #6
    // wires the Goal.RunFrom helper that does this.)
    return await bottom.Goal.RunFrom(context, bottom.StepIndex, bottom.ActionIndex);
}
```

**Key shape points:**

- The resume action's input is plain `Data` — not "Envelope," not "Callback."
- Caller invokes the resume with one method: `App.Run(snapshot, ctx)`. No two-phase "run the suspended action, then run remaining steps" dance visible to the caller.
- The answer is fed by the channel into `Context.Variables` (sentinel `!ask.answer`) **before** invoking this resume action. The resume itself stays answer-agnostic. When `App.Run(snapshot)` re-runs the suspended action, the action reads the sentinel and short-circuits to the answer.
- *(Sentinel cleanup tracked in `Documentation/Runtime2/todos.md` — replace `!ask.answer`-via-Variables with a more explicit Answer parameter pattern when output.ask grows structured options.)*

### 6. `Goal.RunFrom(ctx, stepIdx, actionIdx)` helper

Internal continuation helper. Lives on Goal (or Steps — coder picks the cleaner home). Runs:

1. The action at `(stepIdx, actionIdx)` — which on resume re-runs the suspended action; sentinel makes it short-circuit to the answer.
2. Remaining actions in the same step (`actionIdx + 1 .. step.Actions.Count - 1`).
3. Subsequent steps (`stepIdx + 1 .. Steps.Count - 1`) via the normal Steps loop.

Why this exists: the existing `App.Run(action, ctx)` runs a single Call frame and stops — that's its contract, and many in-process callers depend on it. Resume needs to continue past the action, through the step's remaining actions, through subsequent steps. That continuation needs an explicit method that knows the (stepIdx, actionIdx) anchor. Implementation hides behind `App.Run(Snapshot, ctx)` — callers don't see it.

Suggested shape:

```csharp
// PLang/App/Goals/Goal/this.RunFrom.cs
public partial class @this
{
    public async Task<Data.@this> RunFrom(Actor.Context.@this ctx, int stepIdx, int actionIdx)
    {
        var step = Steps[stepIdx];
        var result = await step.RunFrom(ctx, actionIdx);             // runs action[actionIdx..]
        if (!result.Success && !result.Handled) return result;
        if (result.Returned) return result;
        if (result.Type?.ClrType?.Exit() == true) return result;     // re-suspend if it asks again
        return await Steps.RunAsync(ctx, fromIndex: stepIdx + 1);    // remaining steps
    }
}
```

`Step.RunFrom(ctx, fromActionIdx)` is the within-step equivalent — coder adds it next to `Step.RunAsync`. Existing `Steps.RunAsync(ctx, fromIndex)` overload is still needed (the inner loop call), now justified by this consumer.

`Goal.RunFrom`'s body uses three checks today (failure / Returned / Exit). Deliverable #7 collapses them into one call (`ShouldExit`) — the body shrinks to two lines after that lands.

### 7. `Data.ShouldExit` extension — collapse the engine's stop checks

The step / goal / RunFrom loops all have the same three-line pattern:

```csharp
if (!result.Success && !result.Handled) return result;
if (result.Returned) return result;
if (result.Type?.ClrType?.Exit() == true) return result;
```

Each check has distinct semantics (unhandled failure / explicit goal.return / action-suspends-goal), so they can't merge into one flag — but the call-site pattern repeats everywhere. Wrap it in one extension:

```csharp
// PLang/App/Data/ShouldExit.cs (or wherever Data extensions live)
public static class DataShouldExitExtensions
{
    /// <summary>
    /// True when this Data's presence should stop the surrounding loop.
    /// Combines the three distinct stop conditions: unhandled failure,
    /// explicit goal.return, and Exit-typed result (action wants to suspend).
    /// </summary>
    public static bool ShouldExit(this Data.@this d) =>
        (!d.Success && !d.Handled)
        || d.Returned
        || (d.Type?.ClrType?.Exit() == true);
}
```

Step loop, Steps loop, and `Goal.RunFrom` collapse:

```csharp
if (result.ShouldExit()) return result;
```

Flags stay distinct (consumers downstream still differentiate via `Type.Exit()` vs `Returned` etc.); only the loop-stop check unifies.

**Tests:**
- Each individual flag's `ShouldExit() == true`.
- Combined cases (e.g. Returned + Exit-typed simultaneously — should still stop).

### 8. Drop dead code

After 1–7 land and tests pass:

- Delete `PLang/App/Callback/ICallback.cs`.
- Delete `PLang/App/Callback/AskCallback.cs`.
- Delete `PLang/App/Callback/ErrorCallback.cs`.
- Delete `PLang/App/Callback/Wire/` (per-callback wire shapes fold into Snapshot's existing serializer).
- Update `PLang/App/Errors/Error.cs:55` — `Callback` property → `Snapshot` property (`Data<Snapshot>`). Consumers update accordingly. No production callers outside Wire serializer comments today.
- Update tests under `PLang.Tests/App/CallbackTests/` — round-trip tests rewrite to exercise Snapshot serialization + the single resume entry; per-callback Deserialize tests delete.

### 9. Tests (integration)

- **Stream / stateful, mid-goal:** `- ask user "name?", write to %name%` / `- write out %name%` against a Stream channel piped with `"Alice"` on stdin. Goal completes; `%name% == "Alice"`; no Snapshot ever captured.
- **Message / stateless, mid-goal:** same goal against a fake Message-like channel. Step 1 returns `Data<Ask>`. Step loop short-circuits, captures Snapshot. Goal returns the Ask Data with Snapshot attached. Invoke the resume entry with `{ snapshot, answer:"Alice" }`. After resume, `%name% == "Alice"`. Step 2 ran with the bound value.
- **Permission integration is stage 2b's job.**

## Dependencies

- Existing `App.Snapshot.@this` and `App.CallStack.this.Snapshot.cs` (Capture + Restore).
- Existing `App.Types` (no changes).
- Existing `Channels.Channel.Stream.@this.AskCore` (gets renamed to `Ask` and changes signature to take the `modules.output.ask` action directly; body keeps its stdin-block logic).
- No new packages.

## Acceptance

- Single resume mechanism: `App.Restore(snapshot, ctx)` is the only path the channel/host uses to reconstitute a suspended goal. No `ICallback.Run` dispatch anywhere.
- `Type.Exit()` is the only engine-side discriminator for "this Data exits the goal."
- `output.ask` is ~10 lines: resume-consume sentinel + delegate to the channel's `Ask`.
- Mid-goal `output.ask` works on Stream (no Snapshot involved) and on Message (Snapshot round-trip).
- `dotnet run --project PLang.Tests` zero regressions. Existing `Tests/Callback/AskWithVars` and `AskVarsResumeBindsValue` adapted to the new shape.

## Snapshot wire shape (reference)

The full `App.Snapshot()` walks every `ISnapshot`-implementing subsystem (`App/this.Snapshot.cs:16-27`): Variables, Errors, Providers, Statics, Build, Testing, CallStack. The in-memory `Snapshot.@this` is a key/value tree (`Snapshot/this.cs`).

A serialized **full** snapshot of a paused `- read /path/to/file.txt` mid-goal looks roughly like:

```json
{
  "Variables": {
    "entries": [
      { "name": "%userId%", "value": "u-42", "type": "string" },
      { "name": "%name%",   "value": null,   "type": "string" }
    ]
  },
  "CallStack": {
    "frames": [
      { "goalPrPath": "/app/start.pr", "goalHash": "abc123",
        "stepIndex": 0, "actionIndex": 0, "id": "f1",
        "variables": { /* per-call diffs */ } }
    ]
  },
  "Errors":    { "trail": [ /* recent errors, audit */ ] },
  "Providers": { /* code-routing config */ },
  "Statics":   { /* statics */ },
  "Build":     { /* builder state — empty at runtime */ },
  "Testing":   { /* tester state — empty when not under test */ }
}
```

For a **stateless ask-resume** wire, only CallStack + Variables are needed to re-dispatch:

```json
{
  "Variables": {
    "entries": [
      { "name": "%userId%", "value": "u-42", "type": "string" }
    ]
  },
  "CallStack": {
    "frames": [
      { "goalPrPath": "/app/start.pr", "goalHash": "abc123",
        "stepIndex": 0, "actionIndex": 0, "id": "f1" }
    ]
  }
}
```

For an **error-resume** wire, Errors trail is essential too — the caller needs the failure detail to decide how to retry. Stateless serializer for the Error case keeps `Errors` alongside CallStack + Variables.

**Per-channel serializers.** Statefulness vs statelessness isn't an HTTP-specific concept — it's a property of the channel. Each channel kind owns its own serializer (already the pattern via MIME-based serializer selection on `Channels.Serializers`). Stateful (Stream/Session) channels never serialize a Snapshot — they just hold it in memory across the in-process resume. Stateless channels (Message/HTTP and any future kind) declare a serializer that knows which sections to include for their purpose: ask-resume vs error-resume vs anything else.

How the section-filtering happens at the serializer level (one configurable serializer with an allowlist, vs distinct serializer classes per resume kind, vs subsystems-decide) is the coder's call during implementation. Architect's spec: the choice is the channel's.

**Tracked in todos.md:**
- Per-channel serializer for stateless suspend — how to handle error-message detail in the stateless serializer (errors carry richer context than asks).
- App.Snapshot() orchestration → relocate to Snapshot.@this.Capture(ctx) factory (OBP cleanup).

## What stage 2a unblocks

- **Stage 2b** — has working `output.ask` + Snapshot + resume infrastructure to ride on.
- **Mid-goal `output.ask`** — works end-to-end for the first time, both stateful and stateless.
- **Any future Exit-typed kind** — implements `IExitsGoal`, ships through the same machinery. No per-kind callback classes.
