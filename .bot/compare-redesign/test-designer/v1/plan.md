# Test-designer — v1 plan (compare-redesign / typed value model)

## Scope

Translate `architect/plan/test-strategy.md` (five integration cuts) + `architect/plan/test-coverage.md` (per-stage matrix, failure matrix, new-surfaces inventory) into a stubbed test suite that defines the behavioural contract for the coder.

**Branch unit:** Stages 2–6 land as one green unit; Stage 7 rides behind a warning-then-error gate. The suite green-gates the same way: 2–6 rows must pass together at stage-6 exit; Stage 7 rows can land as the gate flips.

**Test layer rule (from `test-strategy.md`):**
- **C# TUnit (`PLang.Tests/App/CompareRedesign/`)** owns engine-internal behaviour — the door, the `.`/`!` resolver, per-type `Compare`, narrow-on-examination, type-owned `Write`, the lazy param path, async navigation, the gate, renames.
- **PLang `.goal` (`Tests/CompareRedesign/`)** owns developer-facing surface — `if`/`sort`/`contains`/`assert`, `read`/`write out`, `.`/`!` navigation, cross-type, null, membership.
- **Integration cuts** are end-to-end `.goal` tests that need C# instrumentation (a read counter via `Data.MaterializeCount`).

## Folder layout

```
PLang.Tests/App/CompareRedesign/
  Stage1_ComparisonEnumTests.cs
  Stage2_ValueDoorTests.cs
  Stage2_PlaneResolverTests.cs
  Stage2_GetParameterLazyTests.cs
  Stage2_NavigationAsyncTests.cs
  Stage3_ReferenceNarrowTests.cs
  Stage3_PathDemolitionTests.cs
  Stage4_PerTypeCompareTests.cs
  Stage4_RankTests.cs
  Stage5_DataCompareEntryTests.cs
  Stage6_ConsumersTests.cs
  Stage6_DiffRenameTests.cs
  Stage7_SurfaceGateTests.cs
  Stage7_PathGrowthTests.cs

Tests/CompareRedesign/
  Cut1_CrossTypeAntisymmetry/Cut1.test.goal
  Cut2_LazyReadAndNarrow/Cut2.test.goal       (uses C# read-counter assertion)
  Cut3_WriteOutDirIsListing/Cut3.test.goal
  Cut4_SortByIoKey/Cut4.test.goal
  Cut5_EnumBoundaryAndMembership/Cut5.test.goal
  Cut6_ReadThenScalarYieldsContent/Cut6.test.goal
  Failure_DictOrderNumber/...test.goal       (dict > number — error)
  Failure_DictEqNumber/...test.goal           (dict == number — error)
  Failure_DictOrderDict/...test.goal          (dict < dict — error)
  Failure_SortMixedIncomparable/...test.goal
  Plane_DataKeyVsProperty/...test.goal        (%x.size% vs %x!size%)
  Plane_NullComparisonsWork/...test.goal
  Plane_MembershipNeverErrors/...test.goal
  ParamResolutionError/...test.goal
  Read_ScalarStillYieldsContent/...test.goal
  Narrow_ChainWideBangBothBranches/...test.goal
```

C# count: ~55 tests across 14 files. PLang count: ~14 goal tests (6 integration cuts + 8 narrower).

## Batches (~10 tests each, interactive approval)

1. **Batch 1 — Stage 1 enum + Stage 2 door core (C#).** Comparison enum shape; `Value()` sync-complete when present; async only when pending; `Peek()` unparsed rung; `_raw` dissolved into `binary`/`text`; no public sync `.Value` (negative); no generic `ToRaw` (negative); typed value for an authored scalar; ToString/Equals/GetHashCode never navigate.
2. **Batch 2 — Stage 2 planes + GetParameter lazy + nav async (C#).** `.` data plane / `!` property plane (the type answers both); `%var%` ride as typed; `GetParameter<T>` returns lazy `Data<T>` — getter doesn't read; `await Param.Value()` triggers read; resolution-error guard fires after `await`; ValueTask navigation chain sync-completing in memory; awaited once.
3. **Batch 3 — Stage 3 references (C# + goal).** `read file.txt` → `file`; `read http://…` → `url`; unknown local → generic `file`; content-kind inference; `%x!file!path%` no read; `%x.field%` reads + narrows; identity accumulates `[dict, file, item]`; `!type` headline / `!type.list` chain.
4. **Batch 4 — Stage 3 cont. + Stage 4 per-type (C# + goal).** chain-wide `!` (both narrowed and un-narrowed branch); `directory.list : list<path>`; `text` has no `.Path` (negative); `read %url%` fetches, `%url!file!host%` without fetch; `path` no `Content`/`Source`, `path.Write` emits `_location`; `Type.Rank(other)` returns the higher-ranked type; `text` ordinal CI; `number` numeric across the tower.
5. **Batch 5 — Stage 4 more + Stage 5 entry + Stage 6 ops (C# + goal).** `"10" > 9` (text vs number, antisymmetric); `"5" == 5`; date/time/datetime/duration order; `list` lexicographic; `bool`/`choice`/`dict` equality; `null` vs any → `Equal`/`NotEqual`, nulls last; `data.Compare` caller-order; ranking never forces a read; `if`/`assert` boundary per Stage 1 table.
6. **Batch 6 — Stage 6 sort/membership/Pile-2/Diff + Stage 7 gate (C#).** `sort` orders; `sort by size` two-phase (no hang); `contains`/`indexof`/`unique` match `Equal`, no-match on `NotEqual`/`Incomparable`, never error; Pile-2 sites use typed methods (no `.Value is string`); `Diff` (renamed from golden-diff `Compare`); gate: public `item`-subtype CLR return fails; `IsTruthy → @bool` passes; `internal` plumbing untouched; gated interop exempt; `path.IsUnder`/`path.Kind`.
7. **Batch 7 — Integration cuts + failure matrix (goal).** Cut 1 cross-type antisymmetry; Cut 2 lazy read + narrow + chain-wide `!` (read-counter assertion); Cut 3 `write out %dir%` listing; Cut 4 `sort by size` async key; Cut 5 enum boundary + membership; Cut 6 `read`-then-scalar = content; failure rows: `dict > number`, `dict == number`, `dict < dict`, sort mixed incomparable, param resolution failure → typed error.

## Non-goals (deliberate)

Per the architect, these are not test rows — they're code-review assertions (codeanalyzer / auditor):
- "no `Type.Name` switch in dispatch"
- "reuses the existing name→family routing"
- "each type decides" (the meta-rule)

The build gate (Stage 7) is a build-time failure mode; the test surfaces it via a one-off "writing a public raw-CLR property on an `item.@this` subtype fails compilation / throws" probe — but the gate itself is enforced by the compiler.

## Open

- Confirm folder name `PLang.Tests/App/CompareRedesign/` (not `TypedValueModel/`) — the branch is still `compare-redesign`; renaming is cosmetic and deferred (architect plan §1). Going with `CompareRedesign` to match branch.
- Read-counter pattern (integration Cut 2): assume `Data.MaterializeCount` already exists (architect references it in `test-coverage.md` §3 "Existing surfaces"); confirm wiring in Cut 2 stub.

## Workflow

1. Present Batch 1 (full method signatures + one-line intent).
2. Wait for approval. Fold feedback.
3. Repeat for Batches 2–7.
4. Write all files at once after Batch 7 is approved.
5. Commit, push. Output `verdict.json` and `test-plan.md` (the approved batches).
6. Suggest coder next.
