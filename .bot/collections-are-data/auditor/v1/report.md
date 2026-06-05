# Auditor v1 — collections-are-data — PASS

**Next: Ingi** (merge gate decision — do not merge to main ahead of `signature-as-schema-wrapper`).

## Verdict

PASS. 47-commit refactor stands. All upstream bots (codeanalyzer v4, coder v7, tester v7, security v1) PASS through documented rework; their findings are either closed or filed as standing items, and the merge gate is intact.

## What I checked

- Cross-file traces through the chunk/row list model, native dict, marker-based Data recognition, and the add/set merge boundary.
- The honest-Skipped mechanism (`HasSkipTag` short-circuit) and the disabled signing tests it protects.
- F2 (verify-through-list/goal-call) is fail-closed and tester-confirmed deferred.
- C# 4089/0 (re-ran clean here) + `plang --test` 273 total = 271 pass + 2 skipped + 0 fail (re-ran). Build clean (0 errors).

## Architecture — what holds

- **`@schema:"data"` marker is the one canonical Data recognizer.** `Wire.HasDataMarker`, `@this.IsDataMarked`, and the universal `UnwrapJsonElement` all key off the same `{@schema:"data"}` shape — no more `name+value+type` sniffing. A user map with name/value keys but no marker stays a plain map. The marker is written first by `BeginRecord` and by `Wire.Write` directly, and is depth-capped (`MaxReadDepth=64`) so a marker-bombed deep payload throws `JsonException` rather than stack-overflowing.
- **`dict.@this` / `list.@this` own truthiness, equality, and (list only) ordering** through `IBooleanResolvable` / `IEquatableValue` / `IOrderableValue`. `Compare.Order/AreEqualValues` is the single mediator — both `if a > b` and `sort by …` reach the same path, so they cannot drift. Equality-only types (dict) throw `NotOrderableException` cleanly via `Compare.Order` rather than being silently total-ordered.
- **List is rows under the hood, flattened on read.** `Count`/`Items`/`At`/`Locate` walk rows; `Add` is O(1) and never reads existing rows; `IListLeaf` dispatches dissolve-into-list per-value, not via a type-switch in the container.
- **Sign-if-missing stays inside `Wire.Write`.** Owners don't call `EnsureSigned` at egress; the hash-outer scope is per-instance ref-counted (`AsyncLocal<Dictionary<@this,int>>`) so nested signing during outer hashing composes correctly.
- **OBP holds.** Collections are `app.X` collection types with `@this`/`Set`/`Get`/`Add`/`Items`. No public mutable `List<T>` with rules enforced from outside. `Compare.cs` is the mediator; `ScalarComparer.cs` is the one legal scalar type-switch.

## Observations (not blocking)

### O1 — Dict-in-list aliasing via path-set is reachable

`list.@this.CopyStructure` deep-copies nested LISTS but shares dict elements by reference. The architecture comment in `PLang/app/module/list/add.cs:33–36` justifies this with "Stage 2's rebind means `set %x% = ...` mints a new Data rather than mutating the one the list holds" — true for `set %x%` (top-level rebind), **but** `set %x.y% = 5` routes through `app.variable.list.@this.SetValueOnObject` (`PLang/app/variable/list/this.cs:346–349`) which calls `nativeDict.Set(propertyName, value)` — an **in-place mutation** of the shared dict.

Trace:
```
set %d% = {x: 1}             # %d% holds dict{x:1}
add %d% to %list%            # list._items[N] = Value (same Data; same dict)
set %d.x% = 5                # SetValueOnObject → nativeDict.Set("x", 5) on shared dict
# %list[0].x% now reads 5 — alias write-through
```

The existing regression test `ListTests.Add_List_DoesNotAliasSourceVariable` (lines 70–96) covers list-in-list only; the dict-in-list case is uncovered.

This is consistent with JS/Python object reference semantics and may be intended. If so: tighten the comment in `add.cs:32–36` and `set.cs:24–25` to say "rebind, not path-mutation, gives independence" and add a positive aliasing test (`Add_Dict_AliasesByDesign`) so future readers see the constraint. If not intended: extend `CopyStructure` to also deep-copy dict elements (and document the cost). Worth confirming.

**Files:** `PLang/app/type/list/this.cs:155-163`, `PLang/app/module/list/add.cs:32-40`, `PLang/app/module/list/set.cs:24-28`, `PLang/app/variable/list/this.cs:346-349`.

### O2 — `HasSkipTag` has no regression test

Tester v7 flagged this as minor; auditor concurs. The regex
```csharp
@"^\s*tag\s+this\s+test\s+['""]skip['""]\s*$"
```
in `PLang/app/module/test/discover.cs:200-209` is the gate between "honest Skipped" and "no-op pass." Empirically safe today, but boundaries aren't pinned:

- `tag this test 'flaky'` must NOT match (different tag value)
- `tag this test "skip"` (double-quotes) — claimed to match by the `['""]` class; not exercised
- Step[5] holds the tag — must still detect, regardless of step position
- Tag step with `as` clause or trailing args — must not silently skip

Two positive + two negative tests in `PLang.Tests/App/Modules/test/DiscoverTests.cs` would close it.

### O3 — Standing lows already filed

- **text.Convert default JsonSerializer options** (`PLang/app/type/text/this.Convert.cs:32`) — security F2. Narrow reach today (`Identity.PrivateKey` only); symmetric with the open `Variables.Snapshot` leak. One-line fix.
- **`list.@this.CopyStructure` has no explicit depth guard** (`PLang/app/type/list/this.cs:155`) — security F3. Bounded today by `MaxJsonDepth=128` on the inbound construction path. Latent.

## What I did NOT find

- No silent error swallowing in the new list/dict surfaces (Compare exceptions unwrap through `SortGuarded`; navigators return typed `NotFound`).
- No `System.IO` reaches in production C# in the diffed files; the `path.@this` verbs are the only filesystem boundary.
- No `Console.*` writes in the diffed files.
- No courier reading `.Value` to dispatch (Smell #7) — `Wire.cs` carefully keeps the value sealed; the lift through marker/array arms is shape-driven, not type-introspection on the contained value.
- The `@schema:data` marker on inbound user JSON (e.g. `{"@schema":"data","name":"x","value":...}`) lifts as a Data but carries no actor-bound trust without a valid signature — verify still has to pass. Recognizer-confusion attack is not a forgery vector.
- Sign-if-missing's ref-counted `MarkOuterForHash` correctly composes under nested Hash calls; no leak on Dispose.
- The disabled signing tests are honestly Skipped (registered via the `HasSkipTag` source-text short-circuit *before* the build/freshness/`.pr` checks), not gutted to pass. Stale `.pr` is never read on the skip path.
- No `Random.Shared` or non-determinism in production paths where it would matter for tests (the existing list-navigation `random` accessor is an intentional language feature, not a test-stability hazard).

## Numbers re-verified locally

- `dotnet run --project PLang.Tests` → 4089 passed, 0 failed, 0 skipped, 25s.
- `cd Tests && ../PlangConsole/bin/Debug/net10.0/plang --test` → 273 total, 271 pass, 0 fail, 0 timeout, 0 stale, 2 skipped.
- `dotnet build PlangConsole` → 0 errors, 276 warnings (all CS8604 nullable on generated handler code — pre-existing pattern, not branch regression).

## Next

```
VERDICT: PASS
Next bot: none — merge gate is human-driven.
- O1 (dict aliasing comment / test) and O2 (HasSkipTag regression tests) can be picked up by coder as light follow-ups,
  but neither blocks the merge gate.
- Merge gate carries from security v1: hold collections-are-data behind signature-as-schema-wrapper.
```
