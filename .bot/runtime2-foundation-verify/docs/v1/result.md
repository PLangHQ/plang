# docs — runtime2-foundation-verify v1 result

## Doc gap found and filled

**Gap:** `Documentation/Runtime2/todos.md` line 66 — the 2026-04-27 entry "PLang tests for error.handle recovery-value path" described exactly the work this branch shipped (three `.test.goal` regression pins for `handle.cs` recovery-value semantics), but it lacked the `✅ RESOLVED` header its peer entries got. Future readers consulting `todos.md` would see this as still-open and possibly re-do the work.

**Fix:** added the resolution header pointing to this branch + stage 6, listed the three test files with their `handle.cs` line refs, and captured the auditor's minor caveat (Test 3 cannot distinguish `return last` from `return Ok()` because `variable.set` returns `Ok()` with no `Value` — defer-with-consumer, augment when a downstream surface actually reads the recovery's terminal `Value`). Same archival pattern as the 2026-04-24 and 2026-04-27 entries above it: ✅ marker on heading, short close-out paragraph, original text preserved under "### Original entry (archived)".

**File:** `Documentation/Runtime2/todos.md` (header line 66, body lines 67-75).

## Other surfaces checked — no action needed

- **`Documentation/v0.2/good_to_know.md:176`** already documents `ErrorOrder=GoalFirst` semantics ("if the error goal succeeds, retries are skipped entirely") with a pointer to `PLang/App/modules/error/handle.cs`. That section is exactly the behavior the three new tests pin. No drift.
- **`Documentation/v0.2/debug.md:296-307`** documents `%!callStack.Current.Handled%` ("Set by error.handle.Wrap on recovery success") and `%!error%` lifecycle inside/outside recovery scope. The tests' `%!error% is null` assertions exercise this contract; the doc is accurate.
- **`Documentation/v0.2/callbacks.md:108`** documents `error.App` back-ref for recovery materialisation. Unchanged by this branch.
- **No new C# public surface** — zero XML-doc gaps.
- **No new modules/actions** — zero user-facing PLang doc gaps.
- **No user-visible behavior** — no CHANGELOG entry warranted (these are regression-pin tests, not new features).

## Test embedded documentation — already strong

The three `.test.goal` files each have a `/` comment block that:
- Names the exact `handle.cs` line range being pinned.
- Explains *why* the two-layer assertion (side-effect + `%!error%` null) actually pins both the value-return branch AND the `Handled=true` write.
- For `RetryFirstReturnsRecoveryValue`, calls out the 2026-04-27 symmetry fix by name.

Future maintainers reading these tests get the architectural context inline. No additional docs needed.

## Proposals

- CLAUDE.md proposals: none on this branch.
- Character proposals: none on this branch.

## Verdict

**PASS** — ready to merge.
