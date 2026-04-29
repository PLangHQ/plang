# codeanalyzer — runtime2-generator-obp

## v1 — 2026-04-29

5-pass review of the v4 implementation: resolution → `Data.As<T>(context)`, generator restructure (Discovery + Emission), `App.Run` scaffolding, PLNG001 diagnostic. **Verdict: NEEDS WORK** — 10 MAJOR + 19 MINOR + 9 NIT findings. Top items: `ActionClassInfo` is `class` not `record` (incremental cache promise broken), `__variables` and `__paramData/ParamData()` are dead emission, `AsT_Impl` recursion has no cycle detection, `App.Run` catch deliberately swallows `OperationCanceledException` (load-bearing for `timeout.after` but undocumented). See [v1/summary.md](v1/summary.md) and [v1/result.md](v1/result.md).
