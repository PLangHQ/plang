# coder v13 — close two lazy-deserialize carry-forward test gaps

**Branch state going in:** `type-kind-strict` with `lazy-deserialize` merged
(`d4fdd030c`). Clean rebuild, PLang suite **273/273** deterministic across two
runs, `git status` clean after each — the stage-4 stale-`.pr` reproducibility
blocker that FAILed tester v8 is resolved by the merge.

This pass closes the two test-only carry-forwards flagged by lazy-deserialize's
auditor v2 (F1 coverage asymmetry) and tester v3 (F3 List-arm). Both were
**missing regression pins, not live bugs** — the production fixes already exist;
nothing guarded them.

## What shipped (tests only — no production source touched)

### (a) Set-path twin of the malformed-JSON → MaterializeFailed contract
`PLang.Tests/App/LazyDeserialize/LazyDataTests/MaterialiseErrorPathTests.cs` (+2 tests)

The read seam (`%cfg.host%` navigation on malformed JSON) was pinned by
`Navigation_OnMalformedJson_SurfacesMaterializeFailed_NotNotFound`. The write
seam (`set %cfg.host% = ...`) had the identical fix in
`variable/list/this.cs` (`SetValueOnObjectByPath`) but no test.

- `SetPath_OnMalformedJson_SurfacesMaterializeFailed_NotNotFound` — direct path.
- `SetPath_NestedOnMalformedJson_SurfacesMaterializeFailed_NotNotFound` — deeper
  path (`cfg.a.host`) where the parent arrives already-failed via an intermediate
  `GetChild`.

Contract pinned: a parse failure on a raw-backed parent surfaces
`MaterializeFailed`, not a misleading `NotFound`, on the set path.

### (b) `variable.set` List-arm signature survival
`PLang.Tests/App/LazyDeserialize/IntegrationCutsTests/SignedDataSurvivesVariableSetListTests.cs` (new)

Twin of the `SignedDataSurvivesInList` goal, which only pinned the `list.add`
arm. `set %bundle% = [%signed%]` binds through `variable.set`'s no-type
`ShallowClone`, which shares `_value` by reference, so a signed Data nested in the
list keeps its `Signature` and the element still verifies.

- `SignedDataInListLiteral_SurvivesVariableSet_AndVerifies` — sign → bind list via
  the real `variable.set` handler → read `%bundle[0]%` → signature intact →
  `signing.verify` returns true.

## Decisions / findings

- **Mutation-verified both.** (a): neutralizing the `target == null` guard
  (`variable/list/this.cs:309`) flips both set-path tests to `NotFound` → red;
  passes on revert. (b): swapping the shallow bind for a deep `SnapshotClone`
  drops the `[JsonIgnore]` `Signature` → red; passes on revert.
- **First set-path guard branch is defensive/unreachable.** The
  `MaterializeFailed` branch of the *first* guard
  (`variable/list/this.cs:278`, uninitialized-parent) is not reachable by either
  realistic path — a malformed parent either returns raw bytes (non-null `.Value`,
  skips the guard) or arrives as a `FromError` from `GetChild` (initialized,
  skips it). Both reachable failures surface through the **second** guard
  (`target == null`). The first-guard branch is harmless symmetry; left as-is
  (auditor-added, not this pass's call to remove).
- **C# unit tests, not goal tests.** Matches auditor v2's accepted shape (the
  navigation pin is a C# unit test) — faster, no builder dependency, targets the
  exact contract. Goal-level wire-up is already exercised by the LazyDeserialize
  goal suite.

## Verification

- New tests: `MaterialiseErrorPathTests` 6/6, `SignedDataSurvivesVariableSetListTests` 1/1.
- `git status` clean of production source — only the two test files changed.

## Still parked (unchanged, per Ingi)

- `(table, xlsx)` reader + table renderer — real future feature
  (`Documentation/Runtime2/todos.md`).
- Fully type-driven nested Data — wire-format change; folds into the snapshot
  branch (same wire surface).
- Re-review of the merged `type-kind-strict` + `lazy-deserialize` state — Ingi
  will call it when the branch is declared done.

## Files

- `PLang.Tests/App/LazyDeserialize/LazyDataTests/MaterialiseErrorPathTests.cs` (+2 tests)
- `PLang.Tests/App/LazyDeserialize/IntegrationCutsTests/SignedDataSurvivesVariableSetListTests.cs` (new)
