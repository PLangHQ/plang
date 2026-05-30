# Code Analyzer — summary (singular-namespaces)

**Version:** v3 (re-review of coder's fix for the v2 blocker)

## What this is
Review of the `singular-namespaces` refactor (plural→singular `PLang/app/**` namespaces, `X/list/this.cs` registries, Stage-4 type-entity Entry fold). History on this branch:
- **v1 (FAIL):** type-entity door asymmetry — `app.Type[name]` returned a contextless half-entity with silently-null catalog props; doc claimed door-equivalence; test asserted a manual-stamp workaround. Plus dead enumerators.
- **v2 (FAIL):** coder's door fix (memoized catalog cache) replaced the silent null with a **non-deterministic** null — first-wins `TryAdd` over an unordered type set, so a name collision (`"goal"`) let a barren entry shadow the `Fields`-bearing one. Proof-test failed 8/8 in isolation.
- **v3 (this — PASS):** coder added deterministic collision resolution.

## v3 result: **PASS**

### v2 blocker — FIXED
Cache build now breaks same-name ties by richness rank (`type/list/this.cs:172–205`): Record(Fields)=3 > Enum(Values)=2 > Scalar(Shape/CtorSig)=1 > barren=0; richer entry wins regardless of reflection order. `Rank()` reading fold getters during cache build with `Context==null` is safe (Promote short-circuits, no re-entry).

**Verified the way it failed:** `AppType_IndexByName_Fields_OnRecordType_FoldedFromEntry` in **isolation ×8 → 8 PASS / 0 FAIL** (was 0/8). Clean rebuild; full suite **3694/3694**, both projects build clean.

### v2 finding #2 — was MY false positive
`goal/list.All` is not dead — `GoalsTests.cs:233` (`goals.All`) calls it; my v2 grep was too narrow. Coder corrected the comment accurately. Owned and dropped.

### Latent non-blocking edge
Tiebreak resolves cross-rank only; two equal-rank entries under one name would stay first-wins. Doesn't occur today; both would satisfy `Fields != null` anyway. Optional one-line guard if a second populated type ever claims an existing name.

## Next (PASS → tester)
`run.ps1 tester singular-namespaces "Review the code on branch singular-namespaces" -b singular-namespaces`
