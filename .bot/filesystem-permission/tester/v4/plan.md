# Tester v4 — plan

## Scope
Re-review test quality after coder v4 (closes the 9 tester-v3 findings) and
coder v5 (drops `PermissionRecord.AppId`, un-skips Scenario4). The v3 verdict
was NEEDS-FIXES — three major false-greens plus six minor gaps.

## Steps
1. Read coder v4 + v5 reports; map each claimed fix to a test file.
2. Clean rebuild (stale-binary rule). Run both suites.
3. Mutation-test the three v3 major findings — a fix is only closed if the
   mutation that v3 survived now fails a test:
   - F1: `RootComparison` → `OrdinalIgnoreCase`.
   - F2: `isMove` branch → `File.Copy`.
   - F4/v5: disable persisted `Find` → Scenario4 must fail.
4. Read the v5 production change (AppId removal) for security regressions —
   does dropping the field widen any grant?
5. Read the new/changed test bodies for F3/F5/F6/F7/F8/F9; confirm each
   assertion actually pins the named behavior.
6. Write `v4/result.md`, `v4/verdict.json`, update `test-report.json`,
   `summary.md`, `report.json`.

## Focus
- The v5 code change is real production code, not test code — highest risk.
- Mutation, not coverage: v3's lesson was that line coverage hid the gap.
