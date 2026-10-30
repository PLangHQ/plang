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

## Stage 2b — the rest of the types found, not made
- `type.Kinded(string? kind, template? = null)`: the kind by its name (none: the type itself), a template when given;
  a type no list made answers one of its own. `type.list.Made(entry, name, kind, strict, template)` takes the parts
  (no type built to carry them) and answers a kept one with nothing made (TryGetValue before GetOrAdd).
- number's two births take `type?`; none in hand, the operator's {number, kind} (the operator chains to it).
  `list.Count` hands what its type's list answers, so a bare-typed list counts.
- ~45 `list[new type(name, kind), ctx]` sites ask `list[name or class].Kinded(kind[, template])` (26 by a Haiku
  agent, read back). Kept: null-row births, the pick list, the formal reader, the type reader's two identity reads,
  `error.Type`, `data.Value<T>`'s no-asker refusal, the choice and duration operator births (no app), `bool`'s two
  singletons (made twice a process).
- One lazy idiom, allocation-free: `x ?? Interlocked.CompareExchange(ref x, new(...), null) ?? x` for the frame's
  three and for `goal.Broken`, which keeps its broken steps (a list, empty when none).
- Tests: `FrameFirstAskTests` (the tester's two probes, red under a plain check-then-assign), `KindedTypeTests`,
  `TypeReadTests` (a read 24,608 → 9,936 B); BirthTests' call-cost ceilings at measured + 2–4%.

## Stage 3 — a lookup kept by its owner
- The setting store (`actor/setting/this.cs`): `Get(action, option)` keeps its answer at the scope that answers
  (`Keeper`: the nearest with a run value or an actor's rows; a goal call's empty layer reads through), keyed
  (module name, action name, option), at the root's version read first; not kept while any rows are unread (`IsRead`).
  Pin `AnUnsetOption_IsAnsweredOnce_EvenInAFreshChildContext`: 1,856 → 160 B per ask (the same Data).
- `shortcut.Name`: a birth fact. Pin `AShortcutsMatch_MakesNothing`: 5,520 → 0 B.
- `goal.list.Find`: the goals a name may be (and each folder's) kept in one object (`found`), made under the gate
  Add takes, let go whole by Add. Pin `AGoalFoundAgain_ReadsTheKeptGoals`: 3,048 → 1,152 B.
- A frame's `Children` and `property` at their first ask. The frame pin: 1,544 → 680 B.
- Not built, to the architect: the json serializer made per read (its owner would be the context or the read
  context: a new member on a locked surface); the `Func<string, (…View)>` (not found by its text; its stack asked of
  the codeanalyzer).

## Next
- The codeanalyzer measures stage 3; then stage 4 (the generator).
