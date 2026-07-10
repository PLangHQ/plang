# Stage 2–3 status + the `.pr` reader blocker (coder → architect)

Commit `3992ae3e6` on `navigation-driven-record-builder` (pushed).

## Done (Stage 2 born-native lift + test repair)

**Born-native lift landed and working.** Root cause of the builder blocker (and the
"no actions" symptom) was in `type.Build`, not where the plan pointed:

- `type.Build(rawScalar, ctx)`'s last arm routed a **raw CLR scalar** (`int`,
  `DateOnly`, …) straight to the family `Convert` hooks. Those hooks speak
  already-native values and reject raw CLR — `number.Convert` literally does
  `if (value is not item.@this) → "Cannot convert Int32 to number"`.
- Fix: that arm now borns the raw through the lift first, then refines to the
  declared type/kind:
  ```csharp
  // was: return Convert(value, context);
  return Build(global::app.type.@this.Create(value, context), context, format);
  ```
  `type.@this.Create` is now a thin delegator to the `app.type.list` collection
  perimeter (the navigated lift). Its old `OfStatic`/`OwnerOf`/container body moved
  into the perimeter.

**CompareRedesign acceptance green** (except the binary gap below). Rewrote the
tests to the async-reconcile API (int `Rank` property + instance `Compare`).

### Design decision I made — needs your eyes
Two acceptance tests asserted the **old** guarantee "ranking never forces a value
read" (`Rank(Data other) → driver Type`, decided from types alone). The async
reconcile you ruled — `Data.Compare = await (await Value()).Compare(await other.Value())`
— **materializes both operands before ranking** (rank is an int on the *value*).
That guarantee is gone by design. I **inverted** the two tests to assert the new
truth (compare materializes) rather than delete them:
- `Stage4_RankTests.Rank_NeverForcesValueRead` → `Compare_MaterializesBothOperands`
- `Stage5.DataCompare_RankingNeverForcesValueRead` → `DataCompare_MaterializesBeforeRanking`

If lazy-rank (decide the driver from the declared type without reading the value)
is still a requirement, that's a design change — `Rank` would need to be reachable
from `Data.Type` without an item. Flagging so you can veto.

## The `.pr`-fixture failures are OUT OF SCOPE (not a blocker)

~250 failures across all suites share one cause — the `.pr` reader
(`data/reader/this.cs`) now demands a declared type on every value slot, but the
tracked on-disk `.build/*.pr` fixtures predate it (a slot `Name = %path%` with no
type → `MaterializeFailed`). **Ingi's ruling: these are the fixture/plang-test
category and are NOT fixed in this plan — we focus only on the C# unit tests that
don't depend on rebuilt `.pr` fixtures.** So this is not a gate on the collapse;
it is measured out, not chased.

## Also pre-existing (small)
- **Binary compare**: `application/octet-stream` has no registered serializer, so
  `byte[]`→source materialization fails (`Stage4_PerType.BinaryEquality`). The
  `byte[]`→source path is untouched by my work.

## Stage-3 catalog-removal tail (still open, per plan.md)
Perimeter is in. Remaining: delete `convert.Of`/`OfStatic`, reparent the
sub-registries (Kinds/Readers/Renderers/KindHooks/Choices/Scheme) to `app.type.*`,
convert the name/clr indices to FrozenDictionary from `OwnedClrTypes`. Waiting to
know whether you want me on this next or on the `.pr` reader.
