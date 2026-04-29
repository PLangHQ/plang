# test-designer — runtime2-generator-obp

## v1 (2026-04-29) — Test contract for v4

Translated architect v4 plan into 139 C# TUnit test signatures across 18 files. Bodies are `Assert.Fail("Not implemented")`. Spine: matrix tests for every property kind × type-shape combination + dedicated contract tests for the v4 architecture (resolution in `As<T>`, `Data` flows through, generator simpler). Existing `DataResolutionTests.cs` rewritten in place — the old file encoded the opposite contract (snapshot-once `.Value`). Build green. Coder picks up handler stubs, fixture, analyzer wiring, and test-body implementation phase by phase.

See [v1/summary.md](v1/summary.md) for details.
