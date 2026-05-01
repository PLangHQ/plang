# auditor v1 — result on runtime2-data-share-state

## Verdict at a glance

**FAIL — 1 major, 1 minor, 2 nits.**

The four prior reviewers (architect/v1, codeanalyzer v1/v2/v3, tester
v1/v2) approved the data-share-state work, and on a file-by-file basis
the work is sound: the `As<T>` identity rules are correct, `AsCanonical`
is symmetric with the typed walk, `Variables.Set` is honestly dumb
storage, `variable.set`'s MintTyped covers the hot types, the test suite
is honest. C# 2530/2539, plang 166/166.

But the seam between `App/Debug/this.cs:141-160` (placeholder-subscribe
pattern for `--debug={"variables":[...]}`) and the new dumb-storage
`Variables.Set` is broken — and no reviewer traced it.

## Test ground state (verified, not trusted)

- `dotnet run --project PLang.Tests` → 2530/2539, the 9 failures are
  honest `Assert.Fail("Not implemented")` stubs in `ListAddIdentityTests`
  (Phase 5c) and `Plng001PostMigrationTests` (Phase 6). Confirmed.
- `PlangConsole/bin/Debug/net10.0/plang --test` → 166/166 PLang green.
  Confirmed.
- Build clean (364 unrelated nullable warnings, 0 errors).

## Findings

### F1 (major) Cross-file regression: Debug `--debug={"variables":[...]}` watches lose their subscriber after first replacement

**Files:**
- `PLang/App/Debug/this.cs:141-160` (the placeholder pattern)
- `PLang/App/Variables/this.cs:76-86` (Set replacement, dumb storage)
- `PLang/App/modules/variable/set.cs:143-150` (`CarryStateFromSource`)

**Verified empirically.** I dropped a focused TUnit test that mirrors
the placeholder pattern against the real `Variables.Set` + a manual
`CarryStateFromSource`-shape mint. Two consecutive sets on a watched
name fire the placeholder's `OnChange` exactly once, then never again.

```csharp
var placeholder = Data.@this.Uninitialized("x");
int calls = 0;
placeholder.OnChange.Add((_, _) => calls++);
ctx.Variables.Set(placeholder);

var mint1 = new Data.@this<int>("x", 5);  // CarryStateFromSource clones
                                          // from the literal-value Data,
                                          // whose events are empty
ctx.Variables.Set(mint1);                 // → calls == 1 (placeholder fires)

var mint2 = new Data.@this<int>("x", 10);
ctx.Variables.Set(mint2);                 // → calls == 1 (mint1.OnChange empty)
```

Test passes, captured regression. (Test was deleted after verification —
adding a real one is the coder's job; suggested shape below.)

**Why the seam broke.** Before commit `46b327c5` (Phase 1+2),
`Variables.Set` called `dv.CopyEventsFrom(prev)` on replacement — that
copied the placeholder's debug subscriber onto every subsequent binding.
That's what the Debug feature relied on. The commit dropped the call:

```diff
-prev.FireOnChange(dv);
-dv.CopyEventsFrom(prev);
+// Phase 3 will fully rewrite this to dumb storage. For now: fire prev's
+// OnChange (so subscribers see the replacement happen), then drop prev's
+// event lists — no merging onto dv.
+prev.FireOnChange(dv);
```

`CarryStateFromSource` then *replaced* the lost behavior with
"clone from the value's source," which is a different contract:
subscribers attached to *the source value's* Data carry forward, not
subscribers attached to *the target name*. For `set %x% = 5` the source
is the parameter Data with empty events. Result: every fresh binding has
empty events.

**Why no reviewer caught it.**

| Reviewer | What they checked | Why this slipped |
|---|---|---|
| architect/v1 | raised the contract question (plan §Phase 3 line 290) | Ingi made the call to dumb storage; no follow-up audit of who depended on prev-event survival |
| codeanalyzer/v1 | lifecycle audit — *where Data is created* and *where `.Value` is unwrapped* | the inverse audit ("who depends on subscriber survival across replacement?") wasn't run |
| codeanalyzer/v2 | verified the four v1 fixes; flagged the silent `UnwrapJsonElement` unification | scoped to the diff-of-the-fix-commit only |
| codeanalyzer/v3 | nested-var walk + JsonNode dispatch | scoped to coder/v2's two-bug fix |
| tester/v1 | suite + coverage + 7 findings (all coverage gaps) | no test exists for the watch feature, so no coverage gap could be found by inspection of test outputs |
| tester/v2 | confirmed v3's predictive false-green | scoped to v2 surface |

The Debug-side change in this branch was purely syntactic (`+=` →
`.Add(...)`). It looks innocuous in isolation. Codeanalyzer v1 even
graded it CLEAN. Without a cross-file trace from `vars.Set(placeholder)`
through `Variables.Set` replacement back to who-fires-what, the
regression is invisible.

**Impact.** Silent regression of a developer-debugging feature. A user
runs `plang --debug={"variables":[{"name":"x","event":"OnChange"}]}`
expecting to see every assignment to `%x%`. They see only the first.
For `OnTypeChange` the loss is more pernicious — types shift between
mints (Phase 4 picks `Data<int>` vs `Data<long>` based on JSON
normalization), but a user watching for type changes sees only the
first transition.

**Suggested fix.** Two viable options:

1. *Restore subscriber-survival on the dv side*. In `Variables.Set` after
   `prev.FireOnChange(dv)`, add (per the original architect plan):
   ```csharp
   // Subscribers attached to the placeholder for `name` survive replacement —
   // this is how `--debug={"variables":[...]}` tracks repeated sets.
   if (dv.OnCreate.Count == 0) dv.OnCreate = prev.OnCreate;
   if (dv.OnChange.Count == 0) dv.OnChange = prev.OnChange;
   if (dv.OnDelete.Count == 0) dv.OnDelete = prev.OnDelete;
   ```
   This is a partial revert — only when dv's lists are empty. If dv has
   carried events from a `set %x% = %y%` source, those wins (preserves
   today's CarryStateFromSource intent).
   - Update `SubscriberSurvivalTests.Set_Replace_DoesNotAliasAnyEventList`
     and `Set_PostReplacement_SubscribeViaPrev_NotVisibleViaDv` — the
     "dumb storage" contract those tests pin would change. Discuss with
     Ingi: the dumb-storage stance was explicit, so this needs a
     conscious flip.

2. *Move the Debug subscription off the Data instance onto the Variables
   collection.* `Variables` already has `Context.Events` (a separate
   pub/sub). Add a `Variables.OnSet(name, handler)` / `OnRemove(name,
   handler)` API that survives replacement because it lives at the
   collection layer. Update `Debug/this.cs:141-160` to subscribe via
   that API instead of `vars.Set(placeholder)`. Aligns with the test
   header comment ("If subscriber-survival across name-reassignment is
   ever needed, it lives on the Variables collection, not on individual
   Data").
   - Bigger change, but matches Ingi's explicit architectural stance.
     Doesn't disturb the dumb-storage contract.

**Test gap.** Whichever path is chosen, a test must pin it. Suggested
shape (in addition to whatever covers the chosen fix):

```csharp
[Test]
public async Task DebugWatch_OnChange_FiresOnEveryReplacement()
{
    var ctx = _app.User.Context;
    int calls = 0;
    // Whatever the new API is, register a watch on "x":
    RegisterWatch(ctx, "x", DebugEvent.OnChange, (_, _) => calls++);

    ctx.Variables.Set(new Data.@this<int>("x", 1));
    ctx.Variables.Set(new Data.@this<int>("x", 2));
    ctx.Variables.Set(new Data.@this<int>("x", 3));

    await Assert.That(calls).IsEqualTo(3);  // not 1
}
```

---

### F2 (minor) Inherited from tester/v2: AsCanonical container-walk transient state-aliasing is unpinned

**File:** `PLang/App/Data/this.cs:491-494`
**Test file:** `PLang.Tests/App/DataTests/AsTIdentityTests.cs`

Tester/v2 already flagged this as a confirmed false-green (codeanalyzer/v3
predicted it; tester deletion-test verified). Repeating here only because
the tester decided not to block on it; the auditor stance is the same.

The four state-alias lines (`transient.Properties = Properties;
transient.OnCreate = OnCreate; transient.OnChange = OnChange;
transient.OnDelete = OnDelete;`) on the AsCanonical container-walk
branch can be fully deleted and the suite stays green. Documented
contract for Rule 4c claims aliasing happens — assertions don't pin it.

**Suggested fix:** add the test tester/v2 sketched (one list-case test
with `paramData.Properties.Set("note", "via-source")` and a `ReferenceEquals`
assertion on `paramData.OnChange == canonical.OnChange`). Cheap.

---

### F3 (nit) Inherited from codeanalyzer/v2: `global::App.Data.@this.SnapshotClone(...)` qualifier

**Files:**
- `PLang/App/modules/variable/set.cs:117-118`
- `PLang/App/modules/list/add.cs:56`

Both files use `Data.@this(...)` constructors elsewhere without the
`global::App.Data.@this` prefix. The qualifier is just noise; `Data.@this.SnapshotClone(list)`
resolves correctly. Flagged by codeanalyzer/v2; coder/v2 left it.

---

### F4 (nit) Inherited from codeanalyzer/v1+v2: defensive `??` fallback can never fire

**File:** `PLang/App/modules/variable/set.cs:117-118`

```csharp
List<object?> list => new Data.@this<List<object?>>(name,
    (List<object?>?)global::App.Data.@this.SnapshotClone(list) ?? new List<object?>()) { Context = ctx },
```

`SnapshotClone(non-null List)` cannot return null — Serialize emits at
least `"[]"`, Deserialize→JsonElement(Array), Unwrap→List. The `??` is
unreachable. Drop the fallback so a clean NRE surfaces if STJ behavior
ever diverges.

---

## What I checked that was clean

- **`As<T>` four-rule contract** (`Data/this.cs:641-678`) — same-type
  fast path returns `this` (`:647`); variance fast path uses
  `IsPlangAssignable` for the string-not-iterable carve-out (`:653`);
  IEnumerable target inlines `IsPlangIterable ? value : new[] { value }`
  (`:670`); cross-type via `TypeMapping.TryConvertTo`. Identity is
  preserved per architect §Phase 2.
- **`AsCanonical` symmetric with typed walk** for the container path —
  `IsWalkableContainer` + `WalkContainerVars` shared between both,
  per codeanalyzer/v3.
- **Cycle protection** — `_resolvingValues` HashSet + `ResolveDepthLimit
  = 32` (`Data/this.cs:529-554`) — both string-cycle and expanding-cycle
  protected. Test `AsT_DeepChain_5Levels_ResolvesCorrectly` confirms.
- **`Variables.Set` dumb storage** (`Variables/this.cs:67-87`) — fires
  `prev.FireOnChange(dv)` on replacement, fires `dv.FireOnCreate()` on
  fresh insert, no event-list merging. Honest implementation of the
  documented contract. (Even if the contract has a downstream gap —
  finding F1.)
- **`Variables.Remove` fires `OnDelete`** (`Variables/this.cs:351-360`).
- **`variable.set` MintTyped if-chain** covers the documented hot types;
  reflection cold path exists; `CarryStateFromSource` is internally
  consistent (clones `Properties` deeply, shallow-clones the three
  event lists from the *source* — see F1 for the seam concern).
- **`TypeConverter` JsonArray + JsonNode dispatch** (`TypeConverter.cs:129-138, 354`)
  — codeanalyzer/v3 walked the `set type=json` → `LlmMessage` flow end
  to end; spot-checked a few arms, holds up.
- **`list.add` snapshot via `Data.SnapshotClone`** (`list/add.cs:56`) —
  with the `JsonException | NotSupportedException` catch fallback that
  surfaces via `Debug.Write`. Good failure visibility.
- **9 stub failures are honest** — every one uses
  `Assert.Fail("Not implemented")`. No silent passes.

## Other things I considered but didn't flag

- **`vars.Set` callers besides `variable.set`** — `list.add:38`
  (`Variables.Set(ListName, list)` on the convert-non-list-to-list
  path), `Action/this.cs:173` (`Variables.Set("__data__", result)`),
  `cache/wrap.cs:37`. These bypass MintTyped — they call the
  `Set(string, object?, Type?)` overload which wraps non-Data values
  into a fresh `Data.@this(name, value, type)`. The architect plan
  documents `variable.set` as the "sole binding-mint site for
  user-visible variables." `__data__` is engine infrastructure, the
  list.add convert-path is a fallback for legacy non-list values, and
  cache/wrap restores a cached `__data__`. None of these mint
  user-named bindings the user would `set %x% = ...` on, so the
  "sole" framing holds. Not a finding — but worth a sentence in the
  plan if anyone wonders later.
- **`Variables.Set` non-Data path mutates the existing Data in place
  via `existing.Value = value`** (`Variables/this.cs:91`). That path
  re-uses the prev Data instance — its events DO survive. So
  `Context.Variables.Set("__data__", obj)` (a non-Data write) keeps
  any subscribers attached to the existing `__data__` Data. Inconsistent
  with the Data-replacement path (where events do not survive). Worth
  noting but it's a side-effect of the existing dumb-storage shape, not
  a regression introduced by this branch. Probably out of scope.
- **The 43 sidelined `.test.goal2` files** are documented in coder/v1
  summary as out-of-scope per Ingi. Not a finding.
- **The `IObject` constructor failure-path catch in `TypeConverter.cs:204`**
  catches `Exception ex when ex is not (NRE | OOM | StackOverflow)` —
  this is the right exception-class boundary for an external-CLR call
  surface. Acceptable.

## Previous reviews assessed

| Reviewer | Verdict | Auditor read |
|---|---|---|
| codeanalyzer/v1 | NEEDS WORK (4 cleanups) | agree — the 4 fixes were applied cleanly in 60b8d1f3 |
| codeanalyzer/v2 | CLEAN | agree — quietly noticed UnwrapJsonElement unification at 2 of 3 sites; rightly didn't block |
| codeanalyzer/v3 | PASS | agree — nested-var walk symmetry and JsonNode dispatch are correct |
| tester/v1 | APPROVED (4 major coverage gaps, no false-greens) | agree — gaps are real, no false-greens introduced |
| tester/v2 | PASS (1 confirmed false-green major, 2 minor, 6 v1 carryovers) | agree — false-green on AsCanonical container-walk is real (my F2) |

All five prior verdicts are technically defensible *within their scope*.
The auditor's value-add is the cross-cutting trace none of them ran.

## Suggested next step

**FAIL → coder.** Pick one of the two F1 fix paths (subscriber-restore
on dv vs Variables-collection-level subscription), update Debug to match,
add the suggested test. F2 should ride along. F3/F4 are 5-second
deletions that can ride or wait.
