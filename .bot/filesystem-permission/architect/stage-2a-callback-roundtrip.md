---
name: Stage 2a — Callback round-trip (engine + serializer)
description: Generic ICallback infrastructure — Type predicate, step-loop short-circuit, resume continuation, serializer typed-value reconstruction. Fixes mid-goal output.ask too.
type: stage
---

# Stage 2a: Callback round-trip (engine + serializer)

**Goal:** Make `ICallback` round-trip end-to-end. Today the pieces exist (callback classes compile, `callback.run` dispatches, `Plang/Data.cs` serializer writes envelopes) but four gaps stop the full loop:

1. No predicate the engine can use to recognise a callback Data without decomposing it.
2. The step loop doesn't stop when an action returns a callback — subsequent steps run on the callback object as garbage input.
3. After resume, `ICallback.Run` re-dispatches only the suspended action; the rest of the goal never runs.
4. `Plang/Data.cs` inbound deserialization leaves `Value` as a `JsonElement` instead of reconstructing the typed CLR object, so `callback.run`'s `Value is ICallback` check fails immediately.

Stage 2a fixes all four for **any** callback kind. `AskCallback` gets a real mid-goal continuation it never had. Stage 2b builds `PermissionAskCallback` on top of this foundation.

**Out of scope:** building the HTTP/Web channel. The serializer is generic; whatever channel wires it (CLI today; HTTP when shipped — see `Channel/Message/this.cs:7` "Web extends Message (when shipped)") is a separate branch's concern. Stage 2a makes the bytes correct; the channel can use them whenever it lands.

## What this stage is NOT

- **Not a new channel.** The HTTP/Web channel construction is parked. Items 1–5 below are generic infrastructure usable by any future channel.
- **Not permission-specific.** `FilePermission`, `Path.Authorize`, `PermissionAskCallback` all belong to stage 2b.
- **Not a redesign of `ICallback`.** Existing implementers (`AskCallback`, `ErrorCallback`) keep working; the interface shrinks (see #5) and the engine learns to handle results polymorphically.

## Deliverables

### 1. `IsCallback` extension method on `System.Type`

Lives at `PLang/App/Callback/IsCallback.cs` (next to `ICallback.cs`). Static extension:

```csharp
public static class IsCallbackExtensions
{
    public static bool IsCallback(this System.Type clrType)
        => typeof(global::App.Callback.ICallback).IsAssignableFrom(clrType);
}
```

Single responsibility: predicate knows `ICallback`; nothing else changes its meaning. The Data Type class, the engine step loop, and tests all read through this extension.

**Use sites the coder must wire:**

- Engine step loop short-circuit (#2 below): `result.Type?.ClrType?.IsCallback() == true`.
- Auto-signing in `Data/this.Envelope.cs:58` already does `_value is ICallback`; **leave alone** — that path is correct as a value-side check at the serializer layer, where decomposing into Value is the serializer's job, not the engine's. (The OBP rule is about the engine never decomposing Data, not about the serializer.)

**Tests:**

- `typeof(AskCallback).IsCallback()` → true.
- `typeof(ErrorCallback).IsCallback()` → true.
- `typeof(string).IsCallback()` → false.
- `typeof(byte[]).IsCallback()` → false.
- Returns false for a null/missing CLR resolution (extension over nullable patterns at call sites).

### 2. Step-loop short-circuit on callback result

In `Goals/Goal/Steps/this.cs:RunAsync` (line 141–170 today), add one check alongside the existing `Returned` and failure short-circuits:

```csharp
if (result.Type?.ClrType?.IsCallback() == true) return result;
```

Position: after `await step.RunAsync(context)` returns, alongside the `result.Returned` check. The goal's `RunAsync` propagates the callback Data as the goal's effective output. No further steps execute.

**Tests:**

- Goal with 3 steps where step 1 returns `Data<AskCallback>` — steps 2 and 3 do NOT execute. Goal's result is the AskCallback Data.
- Goal with 3 steps where step 1 returns plain `Data<string>` — steps 2 and 3 DO execute (regression guard).

### 3. Resume continuation in `ICallback.Run`

Today `AskCallback.Run` (`Callback/AskCallback.cs:99`) does `await ctx.App.Run(Position.Action, ctx)` — runs ONE action only. After the resumed action returns, the rest of the goal never runs.

**Change:** after the resumed action succeeds, continue running steps from `Position.StepIndex + 1` to end-of-goal.

The cleanest shape is one of:

- A new `Steps.RunAsync(context, fromIndex)` overload on `Goal/Steps/this.cs`. Existing parameterless version delegates to `(context, 0)`.
- A new method on `Goal/this.cs` such as `RunFromAsync(context, stepIndex)`. Calls the steps iterator with the index.

Coder picks based on which fits the existing call sites better. The call lives in exactly one place — the resume path inside `AskCallback.Run` and any future callback's `Run`.

**Updated `AskCallback.Run` shape (sketch):**

```csharp
public async Task<Data.@this> Run(Context.@this ctx)
{
    // ... existing bind-variables + bind-answer logic ...
    var actionResult = await ctx.App.Run(Position.Action, ctx);
    if (!actionResult.Success) return actionResult;
    // Continue the goal from after the suspended step.
    return await Position.Goal.Steps.RunAsync(ctx, fromIndex: Position.StepIndex + 1);
}
```

If the continued steps themselves return a callback, the step-loop short-circuit from #2 kicks in naturally — the new callback Data propagates up; `callback.run`'s caller is responsible for re-suspending.

**Tests:**

- 3-step goal: `- ask user "name?", write to %name%` / `- set %x% = %name%` / `- assert %x% is "Alice"`. Simulate the resume by directly invoking `AskCallback.Run` with `Answer = "Alice"`. After Run completes, `%x%` is "Alice" — proves steps 2 and 3 executed.
- Resume where the continued steps themselves return a callback — the propagated Data is that new callback, not the answer of the first.
- Resume where `Position.StepIndex + 1` is past the last step — Run returns the resumed action's result cleanly (no out-of-range).

### 4. `Plang/Data.cs` typed-Value reconstruction on inbound

Today `FromEnvelope` (`Channels/Serializers/Serializer/Plang/Data.cs:122-128`) does:

```csharp
var d = new global::App.Data.@this("", env.Value, ...);
```

`env.Value` is a `JsonElement` (or `object?`). So inbound, `Data.Value` is never the typed CLR object — it's a tree of JSON nodes. Any consumer doing `Value is SomeType` fails. `callback.run` at `modules/callback/run.cs:22` is one such consumer and is the proximate reason the wire round-trip is broken today.

**Fix:** use `env.Type` (lowercase name) to look up the CLR type via `App.Types.Clr(env.Type)` and deserialize `env.Value` into that type before constructing the Data.

Sketch:

```csharp
private static global::App.Data.@this FromEnvelope(Envelope env, Actor.Context.@this ctx)
{
    object? typedValue = env.Value;
    if (!string.IsNullOrEmpty(env.Type)
        && ctx.App.Types.Clr(env.Type) is { } clrType
        && env.Value is JsonElement el)
    {
        typedValue = el.Deserialize(clrType, _options);
    }
    var d = new global::App.Data.@this("", typedValue,
        string.IsNullOrEmpty(env.Type) ? null : Type.FromName(env.Type));
    d.Signature = env.Signature;
    return d;
}
```

Requires the Deserialize entry points to flow a Context through. The serializer is constructed with no Context today; coder threads it via the channel that owns the serializer (channels already have `Actor.Context`). If threading Context turns out to be more invasive than expected, an alternative is to inject `App.Types` directly into the serializer at construction.

**Tests:**

- Round-trip `Data<AskCallback>`: serialize → bytes → deserialize → `Value is AskCallback` is true; fields preserved.
- Round-trip `Data<string>`: serialize → bytes → deserialize → `Value is string` is true (regression guard).
- Round-trip with unknown `Type` string: `Value` stays as JsonElement (graceful degrade, no throw).
- Signature survives the round-trip in both directions.

### 5. Route `output.ask` through `AskCore`; channel decides stateful vs stateless

Today `output.ask` (`PLang/App/modules/output/ask.cs:37-81`) **always** builds an `AskCallback` directly and returns it. It bypasses `Channel.AskCore` entirely. Stream channel's `AskCore` (which blocks on stdin and returns `Data.Ok(line)`) is unused. Mid-goal `output.ask` on console therefore goes through the stateless suspend/resume path uselessly instead of just blocking and reading the line.

**Fix:** `output.ask` keeps its resume-consume sentinel, then delegates to the channel.

```csharp
// PLang/App/modules/output/ask.cs
public async Task<Data.@this> Run()
{
    // Resume — unchanged
    var answer = Context.Variables.Get(AnswerVariableName);
    if (answer != null && answer.IsInitialized)
    {
        Context.Variables.Remove(AnswerVariableName);
        return Data.@this.Ok(answer.Value);
    }

    // Delegate to the channel. Stream blocks; Message suspends.
    return await Context.Actor.Channels.Input.AskCore(this);
}
```

**`AskCore` signature change.** Today `AskCore(Data prompt, CancellationToken ct)`. New signature takes an `IAskRequest` so the Channel layer doesn't depend on `App.modules.output`:

```csharp
// PLang/App/Channels/IAskRequest.cs   (new file, lives in Channels — the right dependency direction)
public interface IAskRequest
{
    Data.@this<string> Question { get; }
    Data.@this? Variables { get; }
    Actor.Context.@this Context { get; }
}
```

`output.ask` implements `IAskRequest` (its existing properties already match; just declare the interface on the class).

```csharp
// PLang/App/Channels/Channel/this.cs
public abstract Task<Data.@this> AskCore(IAskRequest request, CancellationToken ct = default);
```

**Stream channel implementation** — pulls `Question.Value`, writes, blocks on stdin, returns the line:

```csharp
public override async Task<Data.@this> AskCore(IAskRequest request, CancellationToken ct = default)
{
    var prompt = Data.@this.Ok(request.Question.Value);
    var writeRes = await WriteCore(prompt, ct);
    if (!writeRes.Success) return writeRes;
    // ... existing read-line + timeout logic from Stream/this.cs:97-120
}
```

**Message channel implementation** — builds the `AskCallback` (the logic relocated from `output.ask`):

```csharp
public override Task<Data.@this> AskCore(IAskRequest request, CancellationToken ct = default)
{
    var ctx      = request.Context;
    var bottom   = ctx.App.CallStack.BottomFrame;
    var captured = ExtractCapturedVars(request.Variables, ctx);

    var cb = new AskCallback
    {
        Position  = bottom,
        ActorName = ctx.Actor?.Name ?? "User",
        Variables = captured
    };
    var data = new Data.@this<AskCallback>("", cb);
    data.Context = ctx;
    return Task.FromResult<Data.@this>(data);
}
```

`ExtractCapturedVars` is the variable-capture logic currently inline in `output.ask:54-69` — moves over as-is.

**Tests:**

- Stream channel: `output.ask` returns `Data.Ok(line)` after stdin input. Blocks. No callback.
- Message channel: `output.ask` returns `Data<AskCallback>`. Position and Variables populated from the action's context.
- `output.ask` is ~10 lines after the change (resume-consume + delegate).
- Mid-goal `output.ask` on console runs synchronously: `- ask user "name?", write to %name%` / `- write out %name%` completes in one process tick when invoked against a Stream channel (no suspend, no callback short-circuit fires because no callback is returned).

**Message channel parking note:** if Message channel implementation lands later (with HTTP work), park its `AskCore` body as a `NotImplementedException` for stage 2a. Stream-channel path is what stage 2a actually exercises. The integration test for the full callback round-trip uses a fake Message-like channel for now.

**Name flag for the coder:** `AskCore` is an OBP smell — noun + verb (`Ask` + `Core`). The method does the work of asking; it shouldn't be named like a property-with-suffix. Picking a better name (`Prompt`, `Solicit`, or — since the method already exists — keep `AskCore` but rename to a plain verb on a refactor pass) is the coder's call during implementation. Flag it; don't block on it.

### 6. Drop `ICallback.Serialize` / per-callback static `Deserialize`

The current interface and concrete callbacks carry their own serialization (`AskCallback.Serialize(ctx)` does encrypted-JSON via `crypto.encrypt`). With #4 above, the channel's serializer does this work generically. Per-callback Serialize/Deserialize become redundant.

**Changes:**

- `ICallback` shrinks to `Position` + `Run`. Drop `Serialize(ctx)`.
- `AskCallback.Serialize(ctx)` and `AskCallback.Deserialize(bytes, ctx)` removed.
- `ErrorCallback.Serialize(ctx)` and `ErrorCallback.Deserialize(bytes, ctx)` removed.
- Tests that exercised these directly are deleted or rewritten to round-trip via the channel serializer.

**Crypto note:** `AskCallback.Serialize` today calls `crypto.encrypt` on its JSON bytes. After this change, crypto applies at the **channel** layer (the whole HTTP body, or whatever the wire is), not at the typed-value level. The channel decides whether to encrypt the response body. The typed-value layer carries only the Plang Data envelope and the signature. This is a behaviour change worth flagging in tests — the v1 encrypt is identity pass-through, so functionally nothing differs, but the architectural seam moves from "callback knows about crypto" to "channel knows about wire encryption."

**Tests:**

- Compile-time: `ICallback` interface no longer declares `Serialize`; no remaining direct callers.
- Round-trip via `Plang/Data.cs` serializer is the only path tested (no per-callback static factories exercised directly).

## Dependencies

- Existing `App.Types` registry (lowercase-name ↔ CLR-type mapping). No changes needed.
- Existing `Plang/Data.cs` serializer (extended in #4).
- Existing `Goals/Goal/Steps/this.cs:RunAsync` (one-line addition in #2, new overload or sibling method in #3).
- No new packages, no schema changes.

## Acceptance

- All four mechanical changes (#1–#4) compile and unit-test green.
- Cleanup (#5) leaves no remaining production callers of removed methods.
- New integration test under `PLang.Tests/App/CallbackTests/` (or a new `MidGoalAskTests.cs`): a 3-step goal where step 1 is `output.ask`, step 2 uses the answer, step 3 asserts. Simulate the full round-trip via the channel serializer (not pre-bound `!ask.answer` — that's the old shortcut). Test must exercise `callback.run` going through `signing.verify`, the typed-Value reconstruction from #4, the resume continuation from #3, and the step-loop short-circuit from #2 on the first dispatch.
- Existing `AskCallbackTests`, `ErrorCallbackTests`, `CallbackRunActionTests`, `ICallbackPositionTests` adapted to the new interface — all green.
- `dotnet run --project PLang.Tests` zero regressions.
- `cd Tests && ../PlangConsole/bin/Debug/net10.0/plang --test` zero regressions. Existing `Tests/Callback/AskWithVars` and `AskVarsResumeBindsValue` updated if their semantics change (the `AskWithVars` test currently relies on the next step running with the callback object as the variable — that semantic is gone after #2; the test gets rewritten to assert end-of-goal callback Data instead).

## What stage 2a unblocks

- **Stage 2b** (`PermissionAskCallback` + `Path.Authorize`) — has working infrastructure to ride on. Just implements a new `ICallback` subtype and a method on Path.
- **Mid-goal `output.ask`** — works end-to-end for the first time.
- **Any future callback kind** (Payment, HTTP-OAuth, …) — same machinery.

## Open question deferred to coder

If the resumed step in #3 itself returns a callback, #2's short-circuit kicks in and the new callback propagates. That's the intended behaviour. Edge case: what if the resumed action runs an infinite loop of callbacks (each resume produces another). v1 has no loop guard — coder may add a counter on Context if it surfaces as a concern; not required by spec.
