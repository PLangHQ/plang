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
    ctx.Variables.Set("!ask.answer", "a")
    snapshot.Resume(ctx)                           ← restore + RunFrom in one
      └─ Restore(snapshot, ctx)
      └─ bottom.Goal.RunFrom(ctx, bottom.StepIndex, bottom.ActionIndex)

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

### 5. Single resume entry — `Snapshot.Run(ctx)`

Today `modules/callback/run.cs` dispatches polymorphically via `ICallback.Run`. After this stage, the resume action delegates to a method on the snapshot itself — Snapshot knows how to resume itself, same shape principle as Action knowing how to run itself:

```csharp
[Action("run", Cacheable = false)]
public partial class run : IContext
{
    /// <summary>The suspended Data sent back by the channel. Carries Signature + Snapshot.</summary>
    [IsNotNull]
    public partial Data.@this Data { get; init; }

    public Task<Data.@this> Run()
    {
        // No explicit signing.verify here — the contract is that any Data
        // we receive has been verified by the wire deserializer at the
        // boundary. If verification fails the deserializer never produces
        // a Data; we'd never reach this action. Data == verified by construction.
        if (Data.Snapshot == null)
            return Task.FromResult(Data.@this.FromError(new ServiceError(
                "Resume invoked on Data without a Snapshot", "NoSnapshot", 400)));

        return Data.Snapshot.Resume(Context);
    }
}
```

**`Snapshot.@this.Resume(ctx)`** is the entry. Restores the call chain, then walks it recursively so each goal in the chain re-enters at the right position. The recursion handles cross-goal continuation — when the suspended sub-goal completes, the parent goal continues from the action after its `call SubGoal`:

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

    if (idx == chain.Count - 1)
    {
        // Bottom frame: re-enter the goal at the suspended (StepIndex, ActionIndex).
        return await frame.Goal.RunFrom(ctx, frame.StepIndex, frame.ActionIndex);
    }

    // Parent frame: its action is a "call SubGoal" that's in flight. Push so
    // children see it as caller (live CallStack rebuilds during resume), then
    // recurse into the next frame to resolve the sub-goal.
    await using var callFrame = ctx.App.CallStack.Push(frame.Action, ctx.Variables);

    var subResult = await ResumeChain(chain, idx + 1, ctx);
    if (subResult.ShouldExit()) return subResult;       // re-suspended → bubble up

    // Sub-goal completed naturally. Continue THIS goal from the action
    // AFTER the call action — handles steps like `- call X, write to %y%`
    // where there are more actions in the call step after the call itself.
    return await frame.Goal.RunFrom(ctx, frame.StepIndex, frame.ActionIndex + 1);
}
```

**Key shape points:**

- The resume action's input is plain `Data` — not "Envelope," not "Callback."
- Caller invokes the resume with one method: `Data.Snapshot.Resume(ctx)`. No two-phase dance visible.
- The answer is fed by the channel into `Context.Variables` (sentinel `!ask.answer`) **before** invoking this resume action. The resume itself stays answer-agnostic. When the resumed action re-runs, it reads the sentinel and short-circuits to the answer.
- Snapshot owns its own Resume (paired with the `Snapshot.@this.Capture(ctx)` factory tracked in todos.md). App stays out of it.
- *(Sentinel cleanup tracked in `Documentation/Runtime2/todos.md` — replace `!ask.answer`-via-Variables with a more explicit Answer parameter pattern when output.ask grows structured options.)*

### 6. `Goal.RunFrom(ctx, stepIdx, actionIdx)` helper

Internal continuation helper. Lives on Goal (or Steps — coder picks the cleaner home). Runs:

1. The action at `(stepIdx, actionIdx)` — which on resume re-runs the suspended action; sentinel makes it short-circuit to the answer.
2. Remaining actions in the same step (`actionIdx + 1 .. step.Actions.Count - 1`).
3. Subsequent steps (`stepIdx + 1 .. Steps.Count - 1`) via the normal Steps loop.

Why this exists: `action.RunAsync(ctx)` runs a single action and stops — that's its contract. Resume needs to continue past the action, through the step's remaining actions, through subsequent steps. That continuation needs an explicit method that knows the (stepIdx, actionIdx) anchor. Implementation hides behind `Snapshot.Resume(ctx)` — callers don't see it.

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

### 7. Action owns its execution — drop `App.Run` / `App.RunAction`

Today `App.Run(action, ctx)` is the entry that pushes onto CallStack and runs an action. `App.RunAction<T>(...)` is a generic wrapper around it. Both are called from two distinct contexts that share machinery:

1. **Step-loop dispatch** — `Action.RunAsync` (Action/this.cs:164) wraps lifecycle + modifiers, then dispatches via `App.Run(this, ctx, cause)`. Real step actions.
2. **C# helpers** — `RunAction<T>(new T { ... })` invoked from inside action handlers (signing, crypto, callback.run, Authorize → output.ask, etc.). Synthetic actions.

These don't really need to share the dispatch entry. **Action knows how to run itself.** Collapse:

**a. `Action.@this` gains a `Synthetic` property.** Defaults to `true` (the common case for inline construction from C#).

```csharp
public partial class @this
{
    public bool Synthetic { get; init; } = true;
}
```

**b. Source generator emits `Synthetic = false` for PR-built actions.** Every action constructed by the goal-loader from a `.pr` file gets the initializer. C# inline construction (`new modules.output.ask { Question = ... }`) leaves it at the default `true`.

**c. `Action.RunAsync(context)` is the single entry.** It does its own push/execute/pop (collapses today's `App.Run` body into the partial class). Lifecycle/Modifiers stay where they are around the dispatch.

Sketch:

```csharp
// Action.@this — the partial class
public async Task<Data.@this> RunAsync(Actor.Context.@this context)
{
    var lifecycle = context.LifecycleFor(this);
    var before = await lifecycle.Before.Run(context, EventType.BeforeAction);
    if (!before.Success) return before;

    Data.@this result;
    if (before.Handled) { result = before; result.Handled = false; }
    else
    {
        Func<Task<Data.@this>> dispatch = async () =>
        {
            var (handler, error) = context.App.Modules.GetCodeGenerated(this);
            if (error != null) return Data.@this.FromError(error);

            CallStack.Call.@this call;
            try { call = context.App.CallStack.Push(this, context.Variables); }
            catch (Errors.CallStackOverflowException ex) { return HandleOverflow(ex, context); }

            await using var _ = call;
            using var _anchor = context.AnchorScope(this);
            return await call.ExecuteAsync(handler!, context);
        };
        result = await Modifiers.RunAsync(dispatch, context);
    }

    if (result.Success) context.Variables.Set("__data__", result);
    var after = await lifecycle.After.Run(context, EventType.AfterAction, this, result);
    if (!after.Success) return after;
    return result;
}
```

**d. `CallStack.Push` reads `action.Synthetic` and stamps the Call frame.** No new parameter; the action carries the metadata.

```csharp
// CallStack/this.cs — Push signature simplifies
public Call.@this Push(ActionEntity action, Variables.@this? variables = null)
{
    // ... existing logic, but the new Call records action.Synthetic
    return new Call.@this(action, caller, this, Flags, caller, variables ?? Variables) {
        Synthetic = action.Synthetic
    };
}
```

**e. `App.Run` and `App.RunAction` deleted.** Every call site updates:

```csharp
// Before
await Context.App.RunAction<modules.output.ask>(askAction, Context);

// After
await askAction.RunAsync(Context);
```

Survey: ~20-30 call sites across signing, crypto, builder code, GoalCall, Data.Envelope (lazy signing), AskCallback/ErrorCallback (the latter two die anyway in deliverable #8). Mechanical.

**f. `cause` parameter dies.** Defined on `Push` and `App.Run` (`CallStack/this.cs:77`, `this.cs:450`) as *"Optional async-cause link (recovery dispatch, event publish)"* — checked, no production caller passes it. Action gives us everything; CallStack.Current.Caller is reachable when needed.

**g. Snapshot capture filters by `action.Synthetic`.** Wire-shape capture drops Call frames whose action is synthetic. In-memory full Snapshot keeps them (debug, telemetry).

Net effect on `Authorize`:

```csharp
// Authorize body now reads:
var askAction = new modules.output.ask { Question = Data<string>.Ok(question) };
var askResult = await askAction.RunAsync(Context);     // Synthetic=true by default
// ... rest of switch ...
```

Push records the synthetic frame. On serialize, the synthetic `output.ask` frame is dropped. On the wire, only the real step frames (file.read, parent goal) ride. Resume restores those, re-dispatches; Authorize re-runs, constructs a fresh synthetic `output.ask`, runs it, reads `!ask.answer`, processes. Clean.

**Tests:**
- `Action.Synthetic` defaults to `true`.
- Source-generated actions for PR steps initialize `Synthetic = false` (assert on a sample generated file).
- Push records the flag onto the Call frame.
- Snapshot capture's CallStack section drops synthetic frames in wire form.
- Round-trip: nested call from C# helper → snapshot → resume — passes.
- `App.Run` and `App.RunAction` symbols don't exist in the codebase after this stage.

### 8. `Data.ShouldExit` extension — collapse the engine's stop checks

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

### 9. Drop dead code

After 1–8 land and tests pass:

- Delete `PLang/App/Callback/ICallback.cs`.
- Delete `PLang/App/Callback/AskCallback.cs`.
- Delete `PLang/App/Callback/ErrorCallback.cs`.
- Delete `PLang/App/Callback/Wire/` (per-callback wire shapes fold into Snapshot's existing serializer).
- Update `PLang/App/Errors/Error.cs:55` — `Callback` property → `Snapshot` property (`Data<Snapshot>`). Consumers update accordingly. No production callers outside Wire serializer comments today.
- Update tests under `PLang.Tests/App/CallbackTests/` — round-trip tests rewrite to exercise Snapshot serialization + the single resume entry; per-callback Deserialize tests delete.

### 10. Tests (integration)

- **Stream / stateful, mid-goal:** `- ask user "name?", write to %name%` / `- write out %name%` against a Stream channel piped with `"Alice"` on stdin. Goal completes; `%name% == "Alice"`; no Snapshot ever captured.
- **Message / stateless, mid-goal:** same goal against a fake Message-like channel. Step 1 returns `Data<Ask>`. Step loop short-circuits, captures Snapshot. Goal returns the Ask Data with Snapshot attached. Invoke the resume entry with `{ snapshot, answer:"Alice" }`. After resume, `%name% == "Alice"`. Step 2 ran with the bound value.
- **Cross-goal continuation (nested suspend / resume):**
  ```
  Start
  - write out "Hello"
  - call AskAQuestion
  - write out "%answer%"

  AskAQuestion
  - write out "Asking"
  - ask user "name?", write to %answer%
  ```
  Against a Message-like channel: suspend fires inside `AskAQuestion`'s ask step. Snapshot captures the full chain `[Start#callStep, AskAQuestion#askStep]`. After resume with answer="Alice":
  - AskAQuestion's ask step completes with `%answer% = "Alice"`; no more steps in AskAQuestion.
  - Recursion unwinds to Start; Start continues at the action after `call AskAQuestion`.
  - `write out "%answer%"` runs and prints "Alice".
  Assertion: full output across capture+resume cycle is `"Hello\nAsking\nAlice"`. Verifies the recursive `ResumeChain` cross-goal continuation works end-to-end.
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
