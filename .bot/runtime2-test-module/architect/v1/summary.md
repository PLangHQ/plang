# v1 Summary — Architect Plan for PLang Test Module

## What this is

A plan for building a proper PLang test runner — the first one PLang developers can actually trust. The current runner (`system/test.goal`) uses PLang `foreach` which has a silent-skip bug; 86 of 143 tests vanish with no error. Beyond that there's no isolation, no coverage, no diagnostics, no timeout, no CI integration. This plan replaces it end to end.

Session was plan-only. No runtime code changed.

## What was done

1. **Merged `runtime2` into `runtime2-test-module`** — 173 commits, zero conflicts (the branch only touched `.bot/` files). Pushed.
2. **Read the current state of the codebase** to ground the plan in reality:
   - `App.@this` is the isolation container (`IAsyncDisposable`, owns everything).
   - `App.Test.@this` exists but is just `bool IsEnabled` — expand to a runner.
   - `AfterAction` lifecycle event already fires — subscription is the right hook.
   - `Modifiers` (`timeout.after`, `cache.on`, `error.handle`) exist and can be reused.
   - `AssertionError` has Expected/Actual/Message but no variable snapshot.
3. **Read the existing tester v1/v2 plans and test-designer review** — reconciled them with the current codebase (plans predated the `Runtime2` → `App` rename).
4. **Design discussion with Ingi** locked the scope:
   - Dropped `test.dependency` (tests call each other for shared setup; ordered deps violate isolation).
   - Dropped `test.skip` (tags cover environmental preconditions; known-broken shouldn't be silenced).
   - File boundary = App boundary. Sub-goals and external calls share the App.
   - `[RequiresCapability(params string[])]` lives on the action handler, not module.
   - Rejected `Data.Action` property. Instead, widen `AfterAction` event payload to `(Action, Data)` — smaller blast radius, branch coverage falls out at fire site.
   - Branch coverage in v1: `condition.if` publishes `Properties["branch_index"]`.

## Key decision: event widening, not Data mutation

The tester/test-designer plans correctly identified that coverage tracking needs access to the specific Action that fired. Ingi floated `Data.Action` as the mechanism — any Data can answer "who made me?" I pushed back: Data is a foundational type (every variable, parameter, return value); adding a property ripples into serialization, clone-family, retention. For the test-runner use case, widening the `AfterAction` event payload gives us exactly what we need with one API change at the fire site:

```csharp
// Before
await lifecycle.After.Run(context, EventType.AfterAction);

// After
await lifecycle.After.Run(context, EventType.AfterAction, this, result);
```

Subscribers get `(Action, Data)` for free. Module.action coverage and branch coverage both land from this single change. Data is untouched.

## v1 scope

1. `Testing` class upgrade — runner, owns Results/Coverage/Config. Configured via `--test={...}`.
2. `test.discover` — scan, load .pr, freshness hash check, tag extraction (user + auto).
3. `test.tag` — user-declared tags, no-op at runtime.
4. `test.run` — C# main loop, fresh App per file, parallel, 30s timeout.
5. `test.report` — console + JSON + JUnit XML + coverage tables.
6. `[RequiresCapability(params string[])]` attribute on action handlers.
7. `Variables.Snapshot()` + `AssertionError.Variables` for failure diagnostics.
8. `AfterAction` payload widened to `(Action, Data)`.
9. `condition.if` publishes `branch_index` in result.
10. `system/test.goal` rewritten — no foreach.
11. Per-test builder version + .pr hash for drift correlation.

Deferred: mutation testing, conditional skip, `.golden.pr` drift detection, tag negation, action-level capability overrides.

## Next

Hand off to **test-designer** — design the test suites that prove v1 works.

## Files

- `.bot/runtime2-test-module/architect/v1/plan.md` — full plan (~420 lines).
- `.bot/runtime2-test-module/report.json` — session record.
