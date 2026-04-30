# codeanalyzer — runtime2-generator-obp

## v1 — 2026-04-29

5-pass review of the v4 implementation: resolution → `Data.As<T>(context)`, generator restructure (Discovery + Emission), `App.Run` scaffolding, PLNG001 diagnostic. **Verdict: NEEDS WORK** — 10 MAJOR + 19 MINOR + 9 NIT findings. Top items: `ActionClassInfo` is `class` not `record` (incremental cache promise broken), `__variables` and `__paramData/ParamData()` are dead emission, `AsT_Impl` recursion has no cycle detection, `App.Run` catch deliberately swallows `OperationCanceledException` (load-bearing for `timeout.after` but undocumented). See [v1/summary.md](v1/summary.md) and [v1/result.md](v1/result.md).

## v2 — 2026-04-30

Reviewed coder's response to v1 (12 findings addressed, 25 deferred with rationale, 1 silently missed). **Verdict: NEEDS WORK** — production fixes (cycle detection, record + EquatableArray, dead emission removal, raw-string emission, comments, cleanups) are all correct and `plang test` is unblocked. But **2 new MAJOR findings**: `IncrementalCacheTests` is unit equality not pipeline-driven, `NoDeadEmissionTests` empirically cannot catch the `__variables`/`__paramData` regressions it was named after — the v1 test-gap concern Ingi raised is not actually closed. See [v2/summary.md](v2/summary.md) and [v2/result.md](v2/result.md).

## v3 — 2026-04-30

Reviewed coder's response to v2 (all 7 findings claimed closed). **Verdict: CLEAN** — all 7 honestly closed: the new heuristic in `NoDeadEmissionTests` arithmetic-correctly catches `__variables`-shape (`reads=0`) and `__paramData/ParamData()` is caught by the cross-file caller scan; `IncrementalCacheTests` now drives `CSharpGeneratorDriver` with tracking names and asserts Cached/Unchanged; depth bound + Step.RunAsync OCE test + cycle value assertions + diagnostic-span widening all pin their contracts. One NIT (Finding 46 — unfiltered `ActionInfoTrackingName` is unused). 2456/2456 C# tests green. Recommend tester next. See [v3/summary.md](v3/summary.md) and [v3/result.md](v3/result.md).
