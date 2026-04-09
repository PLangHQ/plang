# Auditor v1 Summary — system-goals-architecture

## What this is

Cross-cutting integrity audit of the system-goals-architecture branch — a major refactor moving Runtime2 to the App namespace (809 production files, 2025 tests passing). Three previous reviewers approved their slices (codeanalyzer: file-level, tester: test quality, security: attack surfaces). This audit looks at the gaps between them.

## What was done

Reviewed all previous bot reports, verified security fixes, traced cross-file contracts, ran tests (2025 pass), and assessed architectural fit.

**Verdict: FAIL** — 2 critical, 2 major, 2 minor findings.

## Key Findings

### Critical

1. **foreach ignores Returned flag** (`PLang/App/modules/loop/foreach.cs:37`) — GoalSteps.RunAsync correctly checks `result.Returned` and propagates it up. But foreach's Run() only checks `result.Success && result.Handled` — never Returned. A `goal.return` inside a foreach body continues iterating instead of unwinding. This is a semantic contract break between foreach and GoalSteps.

   **Fix:** Add `if (result.Returned) return result;` after line 36.

2. **skipInfrastructure missing on channel output** (`PLang/App/Channels/this.cs:129`) — The security fix added `skipInfrastructure: true` to file.read but missed channel output. `Variables.Resolve(str)` at line 129 resolves %!app% patterns in output data, leaking infrastructure state through channels.

   **Fix:** Pass `skipInfrastructure: true` on line 129.

### Major

3. **All 3 security fixes lack tests** — Binding try-finally, skipInfrastructure, CRLF header sanitization are all code-complete but have zero test coverage. A future refactor could remove all three guards with no test failure.

4. **GoalSteps condition detection is fragile** (`PLang/App/Goals/Goal/Steps/this.cs:103-109`) — Sub-step skipping is triggered by string-matching `step.Actions[0].Module == "condition"`. No action name check, no typed contract. Multiple other modules return bools (list.contains, cache.check, assert.*, signing.verify). Low immediate risk but architecturally brittle.

### Minor

5. **Data.Clone() shallow-copies Signature** — shared reference, not independent copy. Low risk since SignedData is effectively immutable after creation.

6. **Goal.Goals lazy-assigns Parent on access** — ownership set during enumeration, not on Add. All current access goes through the property so no immediate bug.

## Previous Reviewers Assessment

- **Codeanalyzer**: Agree. File-level analysis was thorough. All v2 fixes verified.
- **Tester**: Agree. False-green findings are real. Missed the foreach Returned contract break and security test gaps.
- **Security**: Agree on fixes being code-complete. Disagree on not escalating "no tests" to fail-level. Security fixes without tests are incomplete.

## What needs to happen

Send back to **coder** for:
1. Add `if (result.Returned) return result;` to foreach.cs
2. Add `skipInfrastructure: true` to Channels/this.cs:129
3. Add 3 tests for security fixes (Binding exception, skipInfrastructure, CRLF)
4. Add test for non-condition module returning false (verifies no false sub-step skip)
5. Add test for goal.return inside foreach (verifies loop stops)
