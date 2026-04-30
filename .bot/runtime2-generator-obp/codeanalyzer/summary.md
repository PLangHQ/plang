# codeanalyzer — runtime2-generator-obp

## v1 — 2026-04-29

5-pass review of the v4 implementation: resolution → `Data.As<T>(context)`, generator restructure (Discovery + Emission), `App.Run` scaffolding, PLNG001 diagnostic. **Verdict: NEEDS WORK** — 10 MAJOR + 19 MINOR + 9 NIT findings. Top items: `ActionClassInfo` is `class` not `record` (incremental cache promise broken), `__variables` and `__paramData/ParamData()` are dead emission, `AsT_Impl` recursion has no cycle detection, `App.Run` catch deliberately swallows `OperationCanceledException` (load-bearing for `timeout.after` but undocumented). See [v1/summary.md](v1/summary.md) and [v1/result.md](v1/result.md).

## v2 — 2026-04-30

Reviewed coder's response to v1 (12 findings addressed, 25 deferred with rationale, 1 silently missed). **Verdict: NEEDS WORK** — production fixes (cycle detection, record + EquatableArray, dead emission removal, raw-string emission, comments, cleanups) are all correct and `plang test` is unblocked. But **2 new MAJOR findings**: `IncrementalCacheTests` is unit equality not pipeline-driven, `NoDeadEmissionTests` empirically cannot catch the `__variables`/`__paramData` regressions it was named after — the v1 test-gap concern Ingi raised is not actually closed. See [v2/summary.md](v2/summary.md) and [v2/result.md](v2/result.md).
