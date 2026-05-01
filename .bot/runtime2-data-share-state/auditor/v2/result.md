# auditor v2 — result on runtime2-data-share-state (commit b2969406)

## Verdict at a glance

**PASS — F1 closed; 3 carryovers from v1 unchanged (none blocking); 1 minor
new observation, non-regressing.**

Coder v3 fixed the Debug-watch regression I caught in v1. The fix is
structurally cleaner than my suggested partial-revert: instead of
"subscriber-survival when dv lists are empty," the coder reframed the
contract as **events bound to the name, Properties bound to the Data
instance**, and made the aliasing unconditional. `CarryStateFromSource` is
gone — `variable.set` is pure mint+store, `Variables.Set` owns all state
survival. Single source of truth.

The regression test `DebugWatch_OnChange_FiresOnEveryReplacement` traces
the exact seam I described in v1 (placeholder + subscribers + 3 sets → 3
calls). Verified passing in isolation.

## Test ground state (verified, not trusted)

- `dotnet run --project PLang.Tests` → **2533/2542**, the 9 failures are
  the same baseline `Assert.Fail("Not implemented")` stubs in
  `ListAddIdentityTests` (Phase 5c) and `Plng001PostMigrationTests`
  (Phase 6). Confirmed unchanged from v1 (was 2530/2539; +3 new tests in
  `SubscriberSurvivalTests`, +3 pass).
- `PlangConsole/bin/Debug/net10.0/plang --test` → **166/166** PLang green.
  The 6 `[Fail]` lines in plang output are intentional fixture failures
  (`_fixtures_sensitive/sensitivefail.fixture.goal`,
  `_fixtures_fail/failsvar.fixture.goal`) that other tests consume to
  verify the test runner reports failures correctly. Top-level test
  summary: 166 total, 166 pass.
- Regression test isolated run:
  `--treenode-filter "/*/PLang.Tests.App.VariablesTests/SubscriberSurvivalTests/DebugWatch_OnChange_FiresOnEveryReplacement"`
  → 1/1 pass.
- All 11 `SubscriberSurvivalTests` pass in isolation.
- Build clean.

## F1 — Debug-watch regression: CLOSED

**Fix verified.** The new contract:

```csharp
// PLang/App/Variables/this.cs:78-87
if (_variables.TryGetValue(name, out var prev) && !ReferenceEquals(prev, dv))
{
    dv.OnCreate = prev.OnCreate;
    dv.OnChange = prev.OnChange;
    dv.OnDelete = prev.OnDelete;
    prev.FireOnChange(dv);
}
```

Each Data under a name aliases the *same* event-list refs as the prev
binding. New subscribers added at any point are visible to all subsequent
re-bindings, so debug watches survive any number of replacements.

**Regression test pins the seam.** `SubscriberSurvivalTests:179-192` mirrors
`Debug/this.cs:141-160` exactly:

```csharp
var placeholder = global::App.Data.@this.Uninitialized("x");  // == Debug:145
var calls = 0;
placeholder.OnChange.Add((_, _) => calls++);                 // == Debug:149
ctx.Variables.Set(placeholder);                              // == Debug:159

ctx.Variables.Set(new global::App.Data.@this<int>("x", 1));
ctx.Variables.Set(new global::App.Data.@this<int>("x", 2));
ctx.Variables.Set(new global::App.Data.@this<int>("x", 3));

await Assert.That(calls).IsEqualTo(3);  // was 1 in v1
```

Plus contract tests pin the underlying behavior:
- `Set_Replace_AliasesPrevOnChangeOntoDv` — `ReferenceEquals(prev.OnChange, dv.OnChange)`
- `Set_Replace_AliasesAllEventLists` — same for OnCreate/OnDelete
- `Set_PostReplacement_SubscribeViaPrev_FiresOnFurtherReplacements` —
  `prev.OnChange.Add(...)` after replacement still fires on dv2
- `Set_Replace_DoesNotCarryProperties` — Properties stay per-Data
- `ValueSetter_FiresOnChange` — direct `dv.Value = x` fires OnChange
- `Set_PropertiesNotAliased_NewBindingHasOwnProperties` — confirms Property
  isolation

The contract is fully pinned: events follow the name; Properties stay with
the Data instance; same instance re-Set is a no-op.

## New observations from v3 (non-blocking)

### N1 (minor) `Data.Value` setter fires `OnChange(this)` — handlers see same Data twice on direct mutation

**File:** `PLang/App/Data/this.cs:230-232`

```csharp
set
{
    _value = UnwrapJsonElement(value);
    // ... (Updated, IsInitialized, _type = null) ...
    FireOnChange(this);  // (this, this) — handlers get same Data twice
}
```

The replacement path fires `prev.FireOnChange(dv)` — handlers receive
`(prev, dv)` as two distinct objects with old/new state. The Value-setter
path fires `FireOnChange(this)` — handlers receive `(this, this)` AFTER
`_value` is replaced, so `oldData.Value == newData.Value` (both new) and
`oldData.RawValue?.GetType() == newData.RawValue?.GetType()` always.

**Concrete impact:** Debug's `OnTypeChange` watch
(`Debug/this.cs:152-158`) compares
`oldData.RawValue?.GetType().Name != newData.RawValue?.GetType().Name`
and only logs on transitions. On the Value-setter path, this comparison
is never true → OnTypeChange never fires for in-place mutations.

**Why it's non-blocking:** Who writes via the non-Data path of
`Variables.Set`?
- `Executor.cs:91, 98` — `goalFile` system var (no user watches).
- `http providers` — `!ServiceIdentity` (system `!`-prefixed, not user-visible).
- `loop/foreach.cs:42, 44` — but `item` is a `Data` from `EnumerateItems`,
  so this hits the Data path, not the Value-setter path.
- `list/add.cs:38` — `Variables.Set(ListName, list)` where `list` is a raw
  `List<object?>`. This hits the Value-setter path. But the type doesn't
  change between list.add invocations (List → List), so OnTypeChange has
  nothing to fire on anyway.

User-visible `set %x% = ...` always goes through `variable.set` →
`MintTyped` → `Variables.Set(Data path)` which fires `(prev, dv)`
correctly. So OnChange/OnTypeChange watches on user variables work.

**Suggestion:** if the inconsistency ever bites, capture old state in the
setter:
```csharp
var prevSnap = new @this(Name, _value, _type) { Properties = Properties };
_value = UnwrapJsonElement(value);
// ...
FireOnChange(prevSnap);
```
Wait — but a synthetic prevSnap doesn't fit the (prev, new) contract
either since it's not the *real* prev Data. Cleaner to leave this alone
unless a real consumer needs it. Logging this as awareness, not a fix.

## v1 carryovers (still unaddressed)

### F2 (minor) AsCanonical container-walk transient state-aliasing — unchanged

**File:** `PLang/App/Data/this.cs:491-494` (was 491-494 in v1)

The 4 alias lines on the AsCanonical container-walk transient still aren't
pinned by tests. v1 suggested it ride along with F1; coder v3 didn't pick
it up. Tester/v2 already flagged this as a confirmed false-green;
auditor/v1 inherited it. Not a blocker.

### F3 (nit) `global::App.Data.@this.SnapshotClone(...)` qualifier — unchanged

**Files:** `PLang/App/modules/variable/set.cs:115-116`, `PLang/App/modules/list/add.cs:56`

Cosmetic carryover from codeanalyzer/v2. Still there.

### F4 (nit) Defensive `??` fallback can never fire — unchanged

**File:** `PLang/App/modules/variable/set.cs:115-116`

Cosmetic carryover from codeanalyzer/v1+v2. Still there.

## What I checked that is clean

- **`SubscriberSurvivalTests` rewrite** — the two tests that pinned the
  old "dumb storage" contract (`Set_Replace_DoesNotAliasAnyEventList` and
  `Set_PostReplacement_SubscribeViaPrev_NotVisibleViaDv`) were correctly
  flipped to pin the new contract. Per my v1 finding, this needed to be
  an explicit Ingi-flip — it is, with the test header reframing the
  contract per Ingi's call (2026-05-01).
- **`variable.set` simplification** — `CarryStateFromSource` removed; both
  Run paths reduce to `return Task.FromResult(Context.Variables.Set(typedData))`.
  Single source of truth for state survival is now `Variables.Set`. ✓
- **Properties-don't-carry semantic** — confirmed by reading the consumer:
  `condition.if`'s `branchIndex` is read by `test/run.cs:93` directly from
  the action's result Data, not via name lookup. So `__data__` re-binding
  losing Properties doesn't break the coverage subscriber. The new
  contract is also a fix for the bleed described in the v3 commit message:
  "`__data__` aliasing across steps no longer bleeds stale branchIndex etc."
- **Idempotent Set** — `!ReferenceEquals(prev, dv)` guard avoids
  double-firing when the same instance is set twice. Pinned by
  `Set_SameInstance_NoFire`.
- **Reference-aliasing implications** — `dv.OnChange = prev.OnChange` is
  shallow ref assignment. Subscribers added later are visible from any
  prior alias holding the same list. Tested
  (`Set_PostReplacement_SubscribeViaPrev_FiresOnFurtherReplacements`).
  GC: prev itself is GCable once dropped from the dictionary; the shared
  list ref keeps the subscriber chain alive on dv. Coherent.

## Other things I considered but didn't flag

- **`Context.Variables.Set("__data__", result)` per-step re-binding** —
  result is a Data, hits the Data path; events bound to "__data__" survive
  across steps via aliasing. This means a debug watch on `%__data__%`
  would fire on every action (very noisy but consistent).
- **`Variables.Set` non-Data path consistency** — see N1 above. Not a
  regression; just an inconsistency in (prev, new) shape.
- **Generated TUnit cache files for `DebugWatchRegressionRepro`** —
  stale generator artifacts in `obj/Debug/.../generated/...` from my v1
  deleted repro test. Harmless: no `.cs` source backs them, the test
  metadata won't run. Will get cleaned on next `dotnet clean`.

## Previous reviews assessed (v3 only)

| Reviewer | Verdict | Auditor v2 read |
|---|---|---|
| (no new codeanalyzer/tester runs since v1) | n/a | the v3 fix is small enough that re-running them isn't required; the test additions self-document the new contract |

## Suggested next step

**PASS — green-light merge.** F1 closed and pinned. F2/F3/F4 carryovers
can ride a future cleanup PR. N1 is awareness-only; user-visible paths
are unaffected.

If anything blocks merge, it should be **a fresh security review** (not
on file for this branch — flagged in v1, still not done). The JSON-roundtrip
expansions (TypeConverter JsonNode dispatch, list.add SnapshotClone,
variable.set MintTyped's deep-clone of List/Dict) are new attack surface
worth one pass.
