# codeanalyzer v3 — final pass plan

## What's under review

Cumulative review of `runtime2-cleanup` end-state. v1/v2 covered stages 1–2. Stages 3–27 (25 stages) accumulated without per-stage codeanalyzer pass — Ingi pulled and signalled "all stages done" for a one-shot final review before merge to `runtime2`.

Scope: 218 production files changed across 106 commits (vs `runtime2` merge base).

## Approach

Three parallel surveys via Explore subagents + independent verification:

1. **App-spine OBP smell sweep** (4-question CLAUDE.md checklist) on `App/this.cs`, `Channels/this.cs`, `Modules/this.cs`, `Errors/this.cs`, `KeepAlive/this.cs`, `CallStack/this.cs`.
2. **Tier 5 deep dive** (stages 23–27 — never reviewed). Extra scrutiny on architect's deviations #11–#14: Diagnostics-as-static, Conversion-static-vs-instance, http/Default static-eviction surface, Modules.App back-ref necessity.
3. **Cross-cutting cleanup verification**: banned `Console.*` writes, stale `// Stage N:` comments, residual `IProvider`/`App.Build.`/`app.Variables`, orphan files in Utils/, deleted-folder confirmation, TODO markers added during cleanup.

Plus: clean rebuild + C# test suite + `plang --test` from `Tests/`.

## Verdict criteria

- **PASS** if architect's self-audit (`results.md`) is accurate, OBP smell scan is clean on the spine, Tier 5 judgment calls hold up under Rule C, no new banned patterns introduced, no surface regressions vs the destination tree.
- **NEEDS WORK** for any blocker behavioural bug, OBP shape regression beyond what's acknowledged, banned `Console.*` writes added by the cleanup, or test/build red.

Findings written to `report.md`; verdict to `verdict.json`.
