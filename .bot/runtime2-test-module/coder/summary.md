# Coder — runtime2-test-module

Cross-session summary. One paragraph per version.

## v1 — PLang test module shipped (2026-04-20)

Implemented the v1 PLang test module per the architect's plan against the test-designer's 112-test contract. 11 phases, one commit per phase: foundation data types (TestStatus/TestFile/TestRun/Results/Coverage) + Testing class upgrade; Variables.Snapshot + AssertionError.Variables with 9 assert handlers wired via a shared AssertSnapshot helper; `[RequiresCapability]` attribute applied to http.*/llm.query; AfterAction payload widening to `(Context, Action?, Data?)` with all existing subscribers refactored same commit (Ingi's Q1 decision: one way, not two); `condition.if` publishing `Properties["branchIndex"]`; `test.discover/tag/run/report` handlers under `PLang/App/modules/test/`; rewrite of `system/test.goal` — no foreach, just `discover → run → report`. All 93 C# test-designer tests pass; only 1 pre-existing Query_ToolCall flake remains across the full 2244-test suite. End-to-end verified: `plang --test` on the repo's Tests/ discovered 149 test goals, ran them in parallel, produced module.action + branch coverage, wrote `.test/results.json` with per-test metadata. See [v1/summary.md](v1/summary.md) for file-by-file detail.
