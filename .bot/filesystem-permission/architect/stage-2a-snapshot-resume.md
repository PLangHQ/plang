---
name: Stage 2a — Snapshot-resume (unify suspend/resume; drop callback classes)
description: One mechanism for suspend/resume — Snapshot. ICallback / AskCallback / ErrorCallback dissolve; Ask is just a Type the engine recognises.
type: stage
---

# Stage 2a: Snapshot-resume

**Goal:** Unify all suspend/resume flows through `Snapshot`. Today there are two parallel mechanisms — `AskCallback.Run` (variable-bind + re-dispatch) and `ErrorCallback.Run` (App.Restore + dispatch). Both round-trip exists only as scaffolding (zero production callers outside the Wire serializer comment). Replace both with a single rule: when a step returns a Data whose Type "exits the goal", the engine captures a Snapshot, attaches it to the result, and short-circuits the goal. Whoever holds the result (channel, in-process resumer, wire receiver) restores via `App.Restore(snapshot)` and dispatches from the captured Position.

Stage 2b's `Path.Authorize` rides on top — calls `output.ask`, gets either a synchronous answer (stateful channel) or an Exit-typed Data (stateless channel). No permission-specific machinery.

## What this stage is NOT

- **Not a new channel.** Stream channel is the only one whose ask-blocking path is fully wired today (it's the only place that actually exercises the current `AskCore`); that path is what stage 2a exercises end-to-end. The Message/HTTP channel is parked — its `Ask` body lands when HTTP work happens.
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

Step loop sees result.Type.Exit() == true:
    snapshot = new Snapshot()
    App.Capture(snapshot)
    result.Snapshot = snapshot
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

### 2. `Ask` type — the only Exit-typed Data in stage 2a

Replaces `AskCallback`. Small record at `PLang/App/modules/output/ask.cs` (lowercase per PLang convention — same file as the action; the record sits alongside the partial class).

```csharp
public sealed record Ask(string Question) : global::App.IExitsGoal;
```

That's it. No Position, no Actor, no Variables. Just the question.

The previous `AskCallback` fields (Position, Variables, ActorName-as-string) move into `Snapshot` capture, which already records them via the existing CallStack + Variables snapshotting. ActorName-as-string is replaced by the live `Actor.@this` reference inside the captured CallStack frames.

### 3. Action owns the Snapshot; engine short-circuits on `result.Snapshot != null`

`Data.@this` grows one optional field that serialises (it's part of the wire shape — the channel reads it back on resume):

```csharp
public Snapshot.@this? Snapshot { get; set; }
```

**The action owns capture.** An action that wants to exit the goal builds its Snapshot at the action level — *before* returning, while its own Call frame is still alive. This is essential: at step level, the action frame has already popped and we'd lose which action triggered the Exit (a step can have multiple actions).

`Action.@this` (the partial class actions inherit) grows a `Snapshot()` helper:

```csharp
// PLang/App/Goals/Goal/Steps/Step/Actions/Action/this.Snapshot.cs (or similar)
public partial class @this
{
    /// Captures App state from the action's perspective. Call from inside an
    /// action handler *before* returning an Exit-typed Data. The captured
    /// CallStack ends at this action's live frame.
    public Snapshot.@this Snapshot()
    {
        var snap = new Snapshot.@this();
        Context.App.Capture(snap);
        return snap;
    }
}
```

Action handlers that exit attach the snapshot themselves:

```csharp
// inside Message channel's Ask (replaces today's AskCore)
public override Task<Data.@this> Ask(modules.output.ask action, CancellationToken ct = default)
{
    var ask = new Ask(action.Question.Value);
    var data = new Data.@this<Ask>("", ask);
    data.Context = action.Context;
    data.Snapshot = action.Snapshot();      // capture at action level, frame alive
    return Task.FromResult<Data.@this>(data);
}
```

**Step loop adds one branch.** `Steps.RunAsync` (`Goals/Goal/Steps/this.cs:154`) now short-circuits when the result carries a Snapshot — and `result.Type?.ClrType?.Exit() == true` collapses with it, since any Exit-typed result will have an attached Snapshot by contract:

```csharp
result = await step.RunAsync(context);

if (!result.Success && !result.Handled) return result;
if (result.Returned) return result;
if (result.Snapshot != null) return result;    // NEW: action captured a snapshot → suspend
```

**Note on `Type.Exit()`:** the predicate (deliverable #1) is still useful — it's the *invariant* the engine and tests assert (Exit-typed result MUST have a Snapshot; non-Exit-typed result MUST NOT). The step loop checks `Snapshot != null` because the action did the actual capture; the predicate gates which actions are allowed to do so.

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

### 5. Single resume entry (refactor `callback.run` or replace)

Today `modules/callback/run.cs` dispatches polymorphically via `ICallback.Run`. After this stage, dispatch is one path:

```csharp
[Action("run", Cacheable = false)]
public partial class run : IContext
{
    /// <summary>The suspended Data sent back by the channel. Carries Signature + Snapshot.</summary>
    [IsNotNull]
    public partial Data.@this Data { get; init; }

    public async Task<Data.@this> Run()
    {
        // Data has its own signature; verification stays through signing.verify
        // (existing pattern — auditor v2 / security v1 S-F1).
        var v = await Context.App.RunAction<modules.signing.verify>(
            new modules.signing.verify { Data = Data }, Context);
        if (!v.Success) return v;

        if (Data.Snapshot == null)
            return Data.@this.FromError(new ServiceError(
                "Resume invoked on Data without a Snapshot", "NoSnapshot", 400));

        // Restore App state from the suspend.
        Context.App.Restore(Data.Snapshot, Context);

        // Answer (if any) was already set into Context.Variables by the channel
        // *before* invoking this action — e.g. via the !ask.answer sentinel for
        // an Ask resume. This action does not touch the answer; it just restores
        // and re-dispatches. The resumed action reads the sentinel itself.

        var bottom = Context.App.CallStack.BottomFrame;
        if (bottom == null)
            return Data.@this.FromError(new ServiceError(
                "Resume has no bottom frame after Restore", "NoPosition", 400));

        var actionResult = await Context.App.Run(bottom.Action, Context);
        if (!actionResult.Success) return actionResult;

        // Continue the goal from after the suspended step.
        return await bottom.Goal.Steps.RunAsync(Context, fromIndex: bottom.StepIndex + 1);
    }
}
```

**Key shape points:**

- The action input is plain `Data` — not an "Envelope," not a "Callback." Data is what's serialized and what comes back. (Per the OBP rule: Data is a self-owning object, not a wrapper-with-contents.)
- `Data.Snapshot` is the resume state (deliverable #3 wires it onto Data).
- Signature lives on Data itself (existing — `Data.Signature` is already there).
- The answer is fed into the system **by the channel**, not through Data. Channel sets `!ask.answer` (or the relevant sentinel) into `Context.Variables` before invoking this resume action. The resume action stays answer-agnostic; it just restores state and re-dispatches. The resumed action (e.g. `output.ask`) is what reads the sentinel and acts on it.
- Sentinel placement is the channel's contract with the suspended action's resume-consume code. For Ask it's `!ask.answer`. Other kinds may use different sentinels — but Data doesn't carry the answer.

### 6. `Steps.RunAsync(context, fromIndex)` overload

`Steps/this.cs:RunAsync` grows an overload taking a starting index. Existing parameterless version delegates to `(context, 0)`. Single use-site for now: the resume entry above.

### 7. Drop dead code

After 1–6 land and tests pass:

- Delete `PLang/App/Callback/ICallback.cs`.
- Delete `PLang/App/Callback/AskCallback.cs`.
- Delete `PLang/App/Callback/ErrorCallback.cs`.
- Delete `PLang/App/Callback/Wire/` (per-callback wire shapes fold into Snapshot's existing serializer).
- Update `PLang/App/Errors/Error.cs:55` — `Callback` property → `Snapshot` property (`Data<Snapshot>`). Consumers update accordingly. No production callers outside Wire serializer comments today.
- Update tests under `PLang.Tests/App/CallbackTests/` — round-trip tests rewrite to exercise Snapshot serialization + the single resume entry; per-callback Deserialize tests delete.

### 8. Tests (integration)

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

## What stage 2a unblocks

- **Stage 2b** — has working `output.ask` + Snapshot + resume infrastructure to ride on.
- **Mid-goal `output.ask`** — works end-to-end for the first time, both stateful and stateless.
- **Any future Exit-typed kind** — implements `IExitsGoal`, ships through the same machinery. No per-kind callback classes.
