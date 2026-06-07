# Handoff to builder/architect — `DictIsItemKeepsNoOrder` (1 PLang test, design tension)

Coder did the 2 attempts Ingi authorized; the root is a design conflict that needs an
architect call, not a coder patch. Everything else is green (C# 4165/4165, PLang 271/272).

## The test
```
DictIsItemKeepsNoOrder
- set %people% = [{"name":"b"}, {"name":"a"}]
- sort %people%, on error set %caught% = true   (or: on error call Caught)
- assert %caught% is true
```
Intent: sorting a list-of-dict is unorderable; the error must be **catchable** by `on error`.

## What's actually happening (diagnosed)
- The builder compiles the handler correctly: `list.sort | error.handle(Actions=[goal.call Caught])`
  as a **modifier** on `list.sort`. (Same shape as passing tests RecursionDepthLimit,
  DoublePlusDecimal_Errors, TamperedSignedData.)
- `list.sort.Run()` **throws** a raw `Compare.NotOrderableException` (via `list.@this.SortGuarded`)
  — it does NOT return a `Data.FromError`.
- `error.handle` is a `[Modifier]` whose `Wrap(next)` does `var result = await next(); if (!result.Success) … recover`.
  It only inspects a **returned** error. A **thrown** exception from `next()` propagates straight
  past `Wrap` (no try/catch there) to the outer dispatch, which prefixes it `list.sort: …`. So
  `error.handle` never runs the recovery chain → `%caught%` never set → the test fails with the
  raw error.

## The conflict (why it's an architect call)
- Attempt A (coder): make `list.sort.Run()` catch `NotOrderableException` and **return**
  `Data.FromError`. This makes `on error` work — BUT it breaks the C# test
  `SortOnListOfDict_Throws`, which pins the documented "sort over list-of-dict **throws**"
  behavior (collections-are-data settled this). Reverted.
- Attempt B (coder): switch the test to `on error call Caught` (the supported goal-call form).
  No change — same throw-not-caught path.

So: **"sort throws" (design) vs "`on error` can only catch returned errors" (modifier mechanism)**
are in direct conflict. Pick one:

1. **Actions return errors, never throw for data conditions.** Make `list.sort` (and siblings that
   call `SortGuarded`/Compare) return `Data.FromError` on `NotOrderableException`, and update
   `SortOnListOfDict_Throws` to assert a failed `Data` instead of a throw. Cleanest conceptually
   (a data condition is an error value, not an exception), but flips the settled "throws" contract.
2. **`error.handle.Wrap` catches thrown exceptions.** Wrap `await next()` in try/catch, convert a
   caught exception to a `Data.FromError` (mirroring the dispatch's prefixing), then run the normal
   match/recover. Keeps "sort throws", and makes `on error` robust for *every* throwing action — but
   is a broader change to the error pipeline.

Coder's lean: option 2 (on-error should catch thrown exceptions too — that's what a user means by
"on error"), but it's an error-pipeline change that wants an architect sign-off.

## Files
- `PLang/app/module/list/sort.cs` — where the throw originates (`SortByValue`/`SortByField`).
- `PLang/app/type/list/this.cs` `SortGuarded` (~L267) → rethrows `Compare.NotOrderableException`.
- `PLang/app/module/error/handle.cs` `Wrap` (~L83) — the modifier that would need the try/catch (option 2).
- `PLang.Tests/.../SortOnListOfDict_Throws` — the C# test pinning the throw (option 1 must update it).
