# Tester v1 Plan — feature/path-class

## Context
Coder went through 6 iterations building a `Path` class (rich path wrapper). Auditor reviewed twice, approved for merge at v6. 1227/1227 C# tests pass.

## What I'll verify

1. **Run full C# test suite** — confirm 1227 pass (done: all green)
2. **Analyze PathTests (46 tests)** — do they test intent or just implementation?
3. **Analyze FileHandlerTests** — do integration tests verify the full flow?
4. **Check exception-path coverage** — auditor's #1 critical finding was missing try/catch. Coder added it in v6 with 6 try/catch blocks. Are those catch paths tested?
5. **Check overwrite/conflict scenarios** — Copy/Move with Overwrite=true/false when dest exists
6. **Check Save serialization path** — Save has 3 branches (string, byte[], object). Is the object serialization path tested?
7. **Check PLang .goal test existence** — CLAUDE.md requires PLang tests alongside C#
8. **Review developer concerns from v2-v4** — verify tests would catch regressions on OBP violations, System.IO usage, handler thinness

## Deliverables
- `test-report.json` at branch root
- `v1/summary.md` — human-readable findings
- Session entry in `report.json`
