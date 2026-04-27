# Auditor v1 Plan — runtime2-test-module

## Context

Large branch: PLang test module (+10.4k lines, 216 files). Four previous reviewers:
- codeanalyzer v1→v2→v3 (clean on commit d05c138d)
- tester v3→v4→v5→v6 (approved at v6, commit ea7aeb85)
- security v1 (pass, 4 low findings, #3 auto-fixed in 9dc148f5)
- coder v1→v2→v3→v4

My job: find what they missed in the seams.

## Approach

Not re-reviewing individual files — trust prior passes. Focus on cross-cutting integrity:

1. **AfterAction payload widening** — signature changed to `(Context, Action?, Data?)`. Verify ALL subscribers adapted across the repo, not just the ones in coder's diff. One missed caller = null-deref.
2. **Step propagation regression chain** — codeanalyzer v2 caught `?? true` fallback bug; v3 found root cause (SplitAtConditions `_items[i]`). Check if there are OTHER call sites that still bypass the Step-setting indexer.
3. **Goal.LoadedFromPrPath / GetRuntimeDirectory contract** — new property. Any code that calls `Goal.Path` for filesystem resolution must be audited: does it need the runtime directory instead? Check path-relative resolution callers.
4. **PushCancellation / PopCancellation** — new Context methods. Is there a symmetric Pop for every Push? What if an exception fires between them?
5. **Security finding #3 fix (9dc148f5)** — tester didn't re-test; security said "open". Is `Mask` actually called on the diagnostic JSON path? Is there a test proving Sensitive values get masked?
6. **Sensitive masking completeness** — assert fail → `AssertionError.Variables` → test.report JSON. Does masking cover this path end-to-end? What about the console failure block (non-JSON)?
7. **Coverage subscriber — tester F2 in v4**: production coverage subscriber in run.cs was at 0% coverage; tester said tests use a look-alike. Is this still true after v4?
8. **`test.discover` + `test.tag` + `test.run` integration** — smart wrappers (Steps, Actions) own iteration. Any place in test module that still iterates from outside owner? (OBP rule 1/5)
9. **Verdict discipline** — confirm test-report.json F11 "name-claim mismatch" wasn't rubber-stamped by tester. It was flagged minor; should it be major? Deletion test result is telling: deleting `case "junit":` leaves both tests passing.

## Risk ranking

- **F11 name-claim gap** — tester rated minor but per my memory, weak tests with name-claim mismatch are a pattern I flag. This deserves explicit verdict.
- **Sensitive masking** — security fix is 1 day old, unreviewed. High blast-radius (all diagnostic JSON output).
- **Cross-file contract for AfterAction signature** — if any subscriber missed → silent observability loss OR crash.

## Deliverables

- `v1/result.md` — detailed findings
- `v1/verdict.json` — pass/fail
- `.bot/runtime2-test-module/auditor-report.json`

No code changes unless asked. Read-only audit.
