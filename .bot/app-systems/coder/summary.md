# coder — app-systems

**Version:** v10 (stage 9: 9a, 9b and 9c closed through the 9b+9c gate; next 10a)

## What this is
app-systems makes every `app.X` the type X, so the plang path, the C# path and the file path agree. The
architect's plan is `.bot/app-systems/architect/plan.md` (13 stages); its running log of rulings is
`.bot/app-systems/architect/summary.md`. The plan's readable face and its tests are `test/plan/app-systems/`
(`start.md`, `start.goal`, `done.list`). Stage 9 moves every value's birth onto its type and makes each module
action a one-line door to the object that owns the work (worklist:
`.bot/app-systems/architect/plan/stage-9-worklist.md`).

Working mode (Ingi): the architect hands intent; the coder proposes and starts in the same turn, stops only at
a real design fork, and reports each pushed slice for review. Plan tests are intent: each must fail with its
change reverted.

## What was done (v10, all pushed)
- **9a** — births through types; `data<T>.Use` / `data.Use<TAs>` / `data.Follow`; `path.Read` the one read verb.
- **9b** — every module action a one-line door: frames keep only the names they bind (`call.Keeper`); the where
  rule (a field no item has is an error, an optional one filters); identity (one door to an actor); patterns
  B/D/E/F; `module.remove`; mock deleted (mocking is an event); signing; http; `goal.return`.
  Ruled leftovers: debug.tag (one `Tags: dict`, tags on the goal's frame, read `%!callStack.Scope.Tags.x%`,
  the frame's tags a plang dict); code.* (`choice<code.kind>`, `[PlangType("provider")]`, carrying its
  interface); output.ask (every input channel answers an `Ask`, born through `Ask.Create`); the list's
  members take items and the handlers open carriers through `Use`; the LLM trace writes to the debug channel
  (`TraceOutput` and per-call trace files gone); a program's error keeps its key through a channel write.
- **9c** — handlers live at `app/module/<m>/<a>.cs` (no `action/`); the module registry dissolved:
  `%!app.module%` is an empty module holding `list<module>`, the catalog action holds its `Class`,
  `ActionEntry` and shared-instance registration gone.
- **Eval round 25** (C + nano ×3): 64, 64, 63 of 64 — the one silent is `show` step 1, a known weak step
  (silent in round 24 too). The eval tooling read handlers at the pre-9c path; fixed, with the provider kind's
  options mirrored. Pick golden regenerated from round25-run1.
- **dev.sh** — no-op build 9.8 s → 0.017 s (stamp), `full` in its own Gate configuration (analyzers now reach
  PLang), suites in parallel (sweep 339 s → 87 s), the console in `All.proj`.

## The 9b+9c gate (2026-09-28)
- `./dev.sh full` (Gate, analyzers ON), 137 s: Modules 25/954, Types 16/670, Wire 17/466, Data 36/826,
  Generator 10/189, Runtime 19/815 — every suite at or below the morning's counts (30/17/18/36/10/22); none
  cut off. plang: 15 pass, 0 fail (329 stale = goals with no committed .pr).
- Analyzers on PLang now report 257 PLNG003 (raw CLR returns) + 28 PLNG004 (direct System.Text.Json) warnings;
  PLNG001/002 clean.
- Revert checks (mutation, reverted, nothing committed): MisspelledFieldIsAnError, PartialFieldFilters,
  RenderTakesNamedArguments each red on its own mutation (and nothing else); the four frame tests
  (ParameterEndsWithTheCall, LoopItemEndsWithTheLoop, BodyWritesReachTheCaller, CallbackWriteReachesTheCaller)
  red with `Keeper` reverted to "every write stays in its frame". IfReturnReturns was revert-checked at 220.
  The two tag tests pass built fresh (their .pr not committed).
- Fixture DLLs (TestProvider, NoCtorProvider) rebuilt from source; ProviderModuleTests 19/19.

## After the gate (2026-09-29, all pushed; the architect reviews each commit read in full)
- **10a** registration: the plan tests read `%!app.type.list%` / `%!app.type.<name>%`, built and
  revert-checked; `code.load` lost its dead `Name` slot.
- **10b** (A) the type door makes `list<T>` (`list.kind.element`, coined by `kind.Coin`, not held);
  (B) copy-on-write list reads, every write through one door `Change(state, edit)`. (C) — the plan test
  `TypedListHoldsItsType` — waits on Ingi (eager vs lazy element births) and the `list<path>` teaching.
- **Review batch 1** (data/variable/list): EnumerateItems via Follow; `variable.Held`; one door per list
  operation; `item.Enroll` / `item.Spread`; `data.Use<TAs>` follows; `variable.code` owns Ensure/Replace.
- **Review batch 2** (path/http/assert/consent): `action.Warn(Error)` the one build-warning door;
  `path.Read(ctx, bool?)` and url templates born and rendered; `Error.Keeps`; `http` `BaseUrl` an http
  path + `IAddressed.Target()`; one consent door `actor.permission.Ask` (empty and EOF denied); `NoAnswer`.
- Regressions of mine found by name-diffing against `baseline-failures.txt` and fixed: the actor-by-name
  door (CrossActorGoalCall), the path shape test. Gate rule now: diff failing **names**, never counts.
- Next: batch 2 item 6 (`From` → `Takes`, one Create door — shape question open), item 8 (one reference
  base for file/url), then the eval batch (`list<path>`, `code.load`, `BaseUrl`).

## Open / held for Ingi
binding → on (names, hit/miss); decision 236 (variable.set type.Convert); key strictness stays (b); channel
Timeout; `%!app` missing-node reads; environment.start (delete?); timer shape; return depth. The llm cache stays
unfixed on purpose. Pile2_SqliteSettings reads a file gone before this branch.

## Next
10a → 10e, each with its gate. Cadence: `./dev.sh test <Class>` per change (the stamp skips unchanged
builds); `./dev.sh full` once per gate.

## Code example
```csharp
// the owner takes items; the handler opens its carriers
public Task<data.@this> Start() => ListName.Use(name => name.Use<list>(Context,
    list => Index.Use(at => Task.FromResult(list.At(at, Context)))));

// an input channel's answer is an Ask, born through its type
return asked.As(await asked.Value<module.output.Ask>());
```
