# coder — call-cost

## Version
v1: stage 1, made on the first ask (pattern 11). Plan: `.bot/call-cost/coder/v1/plan.md`; the branch's plan:
`.bot/call-cost/architect/plan/start.md`.

## What this is
A plang program pays for work it never asks for. Stage 1 makes three things at their first ask instead of at every
birth or start: the app's step cache, a frame's own variable store / tags / errors, and a goal's broken-step check.

## What was done
- `PLang/app/this.cs`: `Cache => _cache.Value`, a `Lazy<ICache>` (as `store` is); Dispose lets go only a made one. The
  unused `internal set` is gone; the public member and type stay.
- `PLang/app/call/this.cs`: `Parameter`, `Tags`, `Errors` each null until their first ask (`Interlocked.CompareExchange`,
  one made under parallel branches); reads that find none made make nothing (`TryGet`, `Holds`, `Names`, `Error`,
  `Record`'s duplicate check).
- `PLang/app/goal/this.cs`: `Broken()` judged at the goal's first start and kept (`_judged`, `_broken`).
- Pins, `PLang.Tests/Runtime/App/Context/BirthTests.cs` (bytes on this machine, before → after):
  - `ASubAppsStepCache_IsMadeAtItsFirstAsk`: 86,036 → 39,008 (bound 60,000)
  - `AFrameNothingWritesTo_MakesNoStoreTagsOrErrors`: 2,592 → 1,544 (bound 2,000)
  - `AGoalStartedAgain_JudgesItsStepsOnce` (an empty goal): 4,376 → 4,136 (bound 4,256)
  Each read red with its change stashed.
- The bench is the codeanalyzer's (Ingi's rule for the coder).

## Code example
```csharp
public global::app.error.list.@this Errors
    => _errors ?? System.Threading.Interlocked.CompareExchange(ref _errors, new(_stack.Errors), null) ?? _errors;
private global::app.error.list.@this? _errors;
```

## Stage 2 — a type is found, not made (call-cost 3, Ingi's yes)
- `type.Kinded(kind)` (internal): the type's kinded type by the kind's name, made once by the list that made the type
  (`type.list.Made`, now internal, the one birth door; `_full` keeps it).
- number: internal births `(type number, int)` and `(type number, long)` hold the app's kinded type.
- Hot sites by reference: `call.Depth => new(_stack.Number, _depth)` (`call.list.Number` NEW, internal);
  `call.list.Push` compares the frame's `_depth`; `goal.step.list` Start/Rest loop on `CountRaw`;
  `list.Count => new(Type.Types![typeof(number)], CountRaw)`; the bool reader and the type reader's null rows go
  through `Made` with the kind they're handed; `event.Type` is the app's event type.
- Kept: the type reader's lines 25 and 67 (the type read is the data's identity); `error.Type` (an error is often born
  with no app).
- Pin `AGoalRunAgain_FindsItsNumberTypes` (8 steps): 227,552 → 189,456 B (bound 208,000).
- The codeanalyzer's stage-1 lows: one lazy idiom (`x ?? LazyInitializer.EnsureInitialized(ref x, …)`, the flag
  overload for Broken's null answer); a step's Error written tells its goal (`goal.Changed()`), pinned by
  `AGoalWhoseStepIsRefusedAfterAStart_AnswersBrokenAtItsNextStart`; Cache's summary.

## Next
- The codeanalyzer measures stage 2; then stage 3 (a lookup kept by its owner).
