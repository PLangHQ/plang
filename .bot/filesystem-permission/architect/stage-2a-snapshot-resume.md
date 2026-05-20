---
name: Stage 2a — Snapshot-resume (unify suspend/resume; drop callback classes)
description: One mechanism for suspend/resume — Snapshot. ICallback / AskCallback / ErrorCallback dissolve; Ask is just a Type the engine recognises.
type: stage
---

# Stage 2a: Snapshot-resume

**Goal:** Unify all suspend/resume flows through `Snapshot`. Today there are two parallel mechanisms — `AskCallback.Run` (variable-bind + re-dispatch) and `ErrorCallback.Run` (App.Restore + dispatch). Both round-trip exists only as scaffolding (zero production callers outside the Wire serializer comment). Replace both with a single rule: when a step returns a Data whose Type "exits the goal", the engine captures a Snapshot, attaches it to the result, and short-circuits the goal. Whoever holds the result (channel, in-process resumer, wire receiver) restores via `App.Restore(snapshot)` and dispatches from the captured Position.

Stage 2b's `Path.Authorize` rides on top — calls `output.ask`, gets either a synchronous answer (stateful channel) or an Exit-typed Data (stateless channel). No permission-specific machinery.

## What this stage is NOT

- **Not a new channel.** Stream channel is the only one with a working `AskCore` today; that path is what stage 2a exercises end-to-end. The Message/HTTP channel is parked — its `AskCore` body lands when HTTP work happens.
- **Not permission-specific.** `FilePermission`, `Path.Authorize`, all permission semantics belong to stage 2b.
- **Not a new abstraction.** `Snapshot` already exists (`PLang/App/Snapshot/this.cs`, `PLang/App/CallStack/this.Snapshot.cs`). Stage 2a reuses it as the only suspend/resume currency.

## Flow

`- read /path/to/file.txt` mid-goal, no grant, against a stateless channel:

```
file.read action
  ↓ Authorize(verb=Read)                          [stage 2b helper]
  ↓ no grant; output.ask(question="Allow…?")
  ↓ AskCore on Message channel: returns Data<Ask>(question)
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

Against a stateful (Stream) channel the Authorize/output.ask chain runs, but `AskCore` blocks on stdin instead of returning Exit-typed Data. The Snapshot capture / short-circuit path never fires. Goal completes synchronously.

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

Replaces `AskCallback`. Small record at `PLang/App/modules/output/Ask.cs` (the **payload**, not the action; the action `output.ask` returns `Data<Ask>`).

```csharp
public sealed record Ask(string Question) : global::App.IExitsGoal;
```

That's it. No Position, no ActorName, no Variables. Just the question.

The previous `AskCallback` fields (Position, Variables, ActorName) move into `Snapshot` capture, which already records them via the existing CallStack + Variables snapshotting.

### 3. `Data.Snapshot` attachment + step-loop capture

`Data.@this` grows one optional field:

```csharp
[JsonIgnore]
public Snapshot.@this? Snapshot { get; set; }
```

`Steps.RunAsync` (`Goals/Goal/Steps/this.cs:154`) — add one branch alongside the existing failure / Returned checks:

```csharp
result = await step.RunAsync(context);

if (!result.Success && !result.Handled) return result;
if (result.Returned) return result;

// NEW: Exit-typed result captures Snapshot and short-circuits.
if (result.Type?.ClrType?.Exit() == true)
{
    var snap = new Snapshot.@this();
    context.App.Capture(snap);
    result.Snapshot = snap;
    return result;
}
```

`App.Capture(snap)` is the existing snapshot path (`PLang/App/this.Snapshot.cs` / `CallStack/this.Snapshot.cs`).

**Tests:**
- 3-step goal where step 1 returns `Data<Ask>` — steps 2/3 do NOT execute. Returned Data has `.Snapshot` populated; `.Snapshot`'s CallStack frame chain ends at step 1's action.
- 3-step goal where step 1 returns `Data<string>` — steps 2/3 DO execute (regression guard).
- Snapshot capture happens **before** any goal frame pops — verified by reading the snapshot's frame chain, last entry must be the suspended action.

### 4. Route `output.ask` through `AskCore`; remove inline AskCallback construction

Today `output.ask` (`modules/output/ask.cs:37-81`) always builds an `AskCallback` inline. After this stage:

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
    return await Context.Actor.Channels.Input.AskCore(this);
}
```

`AskCore` signature change: takes an `IAskRequest` (lives in `App.Channels` — right dependency direction) so the channel layer doesn't reference `App.modules.output` directly:

```csharp
// PLang/App/Channels/IAskRequest.cs
public interface IAskRequest
{
    Data.@this<string> Question { get; }
    Actor.Context.@this Context { get; }
}
```

`output.ask` implements `IAskRequest`. (No `Variables` member — surviving-variable capture lives in Snapshot now, not the Ask payload.)

**Stream channel `AskCore`** — pulls `Question.Value`, writes, blocks on stdin, returns `Data.Ok(line)`:

```csharp
public override async Task<Data.@this> AskCore(IAskRequest request, CancellationToken ct = default)
{
    var prompt = Data.@this.Ok(request.Question.Value);
    var writeRes = await WriteCore(prompt, ct);
    if (!writeRes.Success) return writeRes;
    // existing read-line + timeout logic from Stream/this.cs:97-120
    return Data.@this.Ok(line ?? string.Empty);
}
```

**Message channel `AskCore`** — returns `Data<Ask>`. No Position / Variables / ActorName here — step-loop Snapshot capture (#3) handles all of that:

```csharp
public override Task<Data.@this> AskCore(IAskRequest request, CancellationToken ct = default)
{
    var ask = new Ask(Question: request.Question.Value);
    var data = new Data.@this<Ask>("", ask);
    data.Context = request.Context;
    return Task.FromResult<Data.@this>(data);
}
```

**Name flag for the coder:** `AskCore` is an OBP smell (noun+verb). Pick a cleaner name during implementation — `Prompt`, `Solicit`, plain `Ask` on the channel, or similar. Architect leaves naming to coder; flagging.

### 5. Single resume entry (refactor `callback.run` or replace)

Today `modules/callback/run.cs` dispatches polymorphically via `ICallback.Run`. After this stage, dispatch is one path:

```csharp
public async Task<Data> Run()        // schematic
{
    // Verify envelope signature (existing)
    var v = await signing.verify(Envelope);
    if (!v.Success) return v;

    var snapshot = Envelope.Snapshot;
    var answer   = Envelope.Answer;     // raw string from channel; may be null

    Context.App.Restore(snapshot, Context);
    if (answer != null)
        Context.Variables.Set(modules.output.ask.AnswerVariableName, answer);

    var bottom = Context.App.CallStack.BottomFrame;
    var actionResult = await Context.App.Run(bottom.Action, Context);
    if (!actionResult.Success) return actionResult;

    // Continue the goal from after the suspended step
    return await bottom.Goal.Steps.RunAsync(Context, fromIndex: bottom.StepIndex + 1);
}
```

The shape of the action's input parameters is the coder's call — the verify, restore, set sentinel, dispatch, continue pieces are the spec.

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
- Existing `Channels.Channel.Stream.@this.AskCore` (signature changes to take `IAskRequest`; body keeps its stdin-block logic).
- No new packages.

## Acceptance

- Single resume mechanism: `App.Restore(snapshot, ctx)` is the only path the channel/host uses to reconstitute a suspended goal. No `ICallback.Run` dispatch anywhere.
- `Type.Exit()` is the only engine-side discriminator for "this Data exits the goal."
- `output.ask` is ~10 lines: resume-consume sentinel + delegate to `AskCore`.
- Mid-goal `output.ask` works on Stream (no Snapshot involved) and on Message (Snapshot round-trip).
- `dotnet run --project PLang.Tests` zero regressions. Existing `Tests/Callback/AskWithVars` and `AskVarsResumeBindsValue` adapted to the new shape.

## What stage 2a unblocks

- **Stage 2b** — has working `output.ask` + Snapshot + resume infrastructure to ride on.
- **Mid-goal `output.ask`** — works end-to-end for the first time, both stateful and stateless.
- **Any future Exit-typed kind** — implements `IExitsGoal`, ships through the same machinery. No per-kind callback classes.
