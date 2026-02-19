# Auditor v4 Plan — Fresh Audit with Test Adequacy Lens

## Context
Auditor v1-v2 reviewed the code and approved for merge. v3 was self-reflection after the tester caught gaps. Now v4 is a fresh audit of the final state (coder v7, 1239 tests passing) applying the new review process: verify code correctness AND test adequacy for every code path.

## What I'll review

1. **Path.cs** — All behavior methods, line by line. For each code branch, confirm a test exercises it.
2. **PathTests.cs** — Assertion quality. Is each assertion specific enough to catch a regression?
3. **File handlers** — OBP compliance (still pure delegators?).
4. **FileHandlerTests.cs** — Handler-level integration tests.
5. **Cross-cutting concerns** — Thread safety, exception handling completeness, edge cases.

## New lens (from v3 learnings)
- For every new code path: "which test hits this line?"
- For every test assertion: "if the code had a subtle bug, would this catch it?"
- Run coverage if I find suspicious gaps.

## Deliverables
- `review-comments.json` — structured findings for the coder
- `v4/summary.md` — full review summary
- `/learnings/feature-path-class/auditor/v4/learnings.md` — any new insights
- Updated `summary.md` (bot root) and `report.json`
