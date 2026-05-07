# Codeanalyzer v2 — runtime2-channels (coder v5 re-check)

**Scope:** All files changed in coder v5 commit (`aa594e5c`)
**Verdict:** FAIL — 2 bugs, 1 latent trap, 2 issues

Tests: C# 2757/2757 pass (up 13 from v4 baseline).

---

## B1 — `InvokeChannelHandler`: null-ctx guard logs but doesn't return early [Bug — F3 not closed]

`PLang/App/Channels/Channel/this.cs:246-249`

```csharp
var ctx = Actor?.Context;
if (ctx == null)
    _ = App?.Debug?.Write($"[Channel '{Name}'] binding {binding.Id} firing with no Actor — handlers receive null ctx");
return binding.Handler(ctx!, null, data);   // ← still called with null ctx
```

The plan specified: write diagnostic, then `return Data.@this.Ok((object?)null)`. The code logs but falls through to `binding.Handler(ctx!, null, data)`. The `!` suppresses the compiler warning; it does nothing at runtime. Every existing handler assumes non-null context (it's the engine's contract). Any binding on a no-Actor channel still NREs inside the handler body rather than at this call site.

The comment "handlers must guard for null themselves" distributes the fix across every handler rather than sealing it here. F3 is not closed.

**Fix:** add `return Data.@this.Ok((object?)null);` immediately after the `Debug.Write` call.

---

## B2 — `RunGoalAsync(GoalCall)` still injects params into shared Variables [Bug — F2 partially applied]

`PLang/App/this.cs:610-612`

```csharp
if (goalCall.Parameters != null)
    foreach (var param in goalCall.Parameters)
        context.Variables.Set(param.Name, param);   // ← actor-shared dict
```

The `Variables.Calls` infrastructure is correctly built. `GoalChannel.InvokeGoal` correctly pushes a frame before calling `RunGoalAsync(Goal, ctx, ct)` (the no-parameter overload). But `RunGoalAsync(GoalCall)` — called by `goal.call`, `http/DefaultHttpProvider`, `llm/OpenAiProvider`, `mock/action`, and `test/run` — still injects directly into shared Variables. Two concurrent `goal.call` actions passing the same parameter name race: last writer wins, wrong goal reads the wrong value.

The plan specified converting this loop to `await using var _ = context.Variables.Calls.Push(goalCall.Parameters)`. The coder's comment says "concurrency boundaries push their own frame" but none of the callers above push a frame before calling this overload.

**Fix:** replace the foreach+Set loop with `await using var _ = context.Variables.Calls.Push(goalCall.Parameters);`. Sequential-call "leaky" semantics (the LoadUser pattern) are still served: a sequential caller reads back parameters pushed on the frame; after the goal returns and the frame disposes, variables from Set inside the goal remain on the underlying dict as before.

---

## L1 — Inside a call frame, goal-body `set` is invisible to subsequent reads [Latent PLang developer trap]

`PLang/App/Variables/this.cs` Get path + `CallsTests.cs:131-142`

The Calls frame is a **read-only overlay**. Inside an active frame, `set %x% = "new"` writes to `_variables["x"]` but `get %x%` still resolves the frame value, not the new assignment. A PLang developer who passes `x=1` into a goal and then does `- set %x% = 2` will find `%x%` still reads `1` for the rest of that goal body. The test at line 131 covers this deliberately, the comment explains it — but it's counter-intuitive enough to cause silent logic bugs.

Not requesting a fix (design is intentional). Needs a note in `Documentation/Runtime2/good_to_know.md` so PLang developers know: **parameters passed into a goal shadow writes of the same name for the lifetime of that call**.

---

## I1 — `Push_FrameInvisibleToParallelFlows` test is sequential, not parallel [Test quality]

`PLang.Tests/App/VariablesTests/CallsTests.cs:71-96`

```csharp
var (gotA, gotB) = await TaskA(vars).ContinueWith(async ta =>
{
    var a = await ta;
    var b = await TaskB(vars);   // B starts only after A completes
    return (a, b);
}).Unwrap();
```

`ContinueWith` chains: B runs after A finishes. AsyncLocal scoping is not tested by sequential calls — the frame is naturally gone before B starts. The test name and comment promise parallel isolation but deliver sequential ordering. The actual parallel isolation test is `Push_ConcurrentFlows_NoRaceOnSharedName` (line 100), which is correct.

Rename `Push_FrameInvisibleToParallelFlows` to something accurate (e.g. `Push_FrameIsGone_AfterDispose_SequentialCaller`) or convert to `Task.WhenAll`.

---

## I2 — `Channel.Events` is `public List<>` — new OBP smell [Design]

`PLang/App/Channels/Channel/this.cs:63`

```csharp
public List<global::App.Events.Lifecycle.Bindings.Binding.@this> Events { get; } = new();
```

`Goal.Events` is an encapsulated `modules.Events` type that owns its own iteration, recursion logic, and binding registration surface. `Channel.Events` is a raw `public List<>`. The recursion guard (`_activeBindings`) and the binding iteration live in `Channel.@this` — enforcement is split from the data across two files (OBP checklist item 1 + item 4).

This is a new smell introduced in v3/v4 and not addressed by v5. Either:
- promote it to a `ChannelEvents` type (preferred, mirrors `modules.Events`), or
- document that this is a deliberate divergence and why.

---

## Closed findings (F1, F3–F6 status)

| Finding | Status |
|---------|--------|
| F1 — Services.Remove race | Closed — `ConcurrentDictionary<Guid, Service>` |
| F2 — GoalChannel %!data% race | Partially closed — GoalChannel path fixed; `RunGoalAsync(GoalCall)` still injects directly |
| F3 — InvokeChannelHandler NRE | Not closed — logs but doesn't return early |
| F4 — EventContext dead code | Closed — file deleted |
| F5 — Stream Encoding | Closed — `ResolveEncoding()` added with fallback |
| F6 — channel.set direction | Closed — `ResolveDirection()` with Bidirectional default |
