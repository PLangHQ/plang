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

## Next
- The codeanalyzer measures stage 1 on the bench; then stage 2 (a type is found, not made).
