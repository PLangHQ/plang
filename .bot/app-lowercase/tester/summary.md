# tester — app-lowercase

## Version
v1 — first tester pass on this branch, run after merging `origin/runtime2`
(5 commits from `runtime2-foundation-verify`) into `app-lowercase`.

## What this is

Integration check, not a code review. The branch already had 12 coder
commits (rename + 7 OBP merges) approved by codeanalyzer. Today's work
was: pull `runtime2` (which had advanced with the foundation-verify
error.handle recovery-value tests + todos.md reconciliation) into
`app-lowercase` and confirm the merge result is healthy.

The merge was conflict-free. My job was to confirm that's real — that
the rename and the new foundation-verify tests compose without surfacing
reflection-discovery surprises, builder false greens, or weak assertions.

## What was done

- Reset a harness-injected staged CLAUDE.md diff before switching branches.
- `git checkout app-lowercase` → `git merge origin/runtime2 --no-edit`: 5
  commits in, no conflicts. Merge commit `f24356380`. Pushed to origin.
- Clean rebuild from zero (`rm -rf bin/obj across all projects`, then
  `dotnet build PlangConsole`): 0 errors, ~52s.
- **C# suite**: 2752/2752 pass (≡ coder v1 baseline at `199b4997`).
- **PLang suite** (`cd Tests && plang --test`): 206 real passes + 6
  pre-existing expected-fail fixtures. Delta vs baseline (203) = +3,
  matching the 3 new tests merged in:
  - `Errors/GoalFirstReturnsRecoveryValue.test.goal`
  - `Errors/RetryFirstReturnsRecoveryValue.test.goal`
  - `Errors/MultiActionRecoveryLastActionPropagates.test.goal`
- **Builder false-green check** on all 3 new `.pr` files. Every step's
  `text` semantically matches `actions[*].module.action`. The
  `error.handle` modifier carries the right `Order` / `RetryCount` /
  `Actions` parameters on each. No shift, no shadow.
- **Assertion quality** on the 3 new tests: each pins both the recovery
  side-effect (`%content%`/`%first%`/`%second%`) AND `%!error% is null`.
  That second pin is the architect's symmetry call — without it, a
  regression that returns `Ok()` without setting `Handled=true` would
  slip through. With it, deletion test catches the bug.
- Pre-existing stderr stack (`Failed to deserialize List\`1 ...`) flipped
  its namespace string from PascalCase to lowercase, exactly as the coder
  v1 baseline predicted. Shape unchanged. Not a regression.

## Files written

- `.bot/app-lowercase/tester/v1/plan.md`
- `.bot/app-lowercase/tester/v1/result.md`
- `.bot/app-lowercase/tester/v1/verdict.json`
- `.bot/app-lowercase/test-report.json`
- `.bot/app-lowercase/report.json` — appended tester session

## Verdict

**PASS.** No findings. The merge is safe; the 3 new tests are honestly
green and would catch the bug the foundation-verify branch was guarding
against.

## Code example — the false-green check that matters

The pattern for *every* PLang test built against a multi-action step:

```
step.text  : "throw error \"boom\", on error set %content% = \"from-recovery\", order GoalFirst"

step.actions[0] : { module: "error", action: "throw",
                    modifiers: [
                      { module: "error", action: "handle",
                        parameters: [
                          { name: "Order",   value: "GoalFirst" },
                          { name: "Actions", value: [
                              { module: "variable", action: "set",
                                parameters: [ {Name:"%content%"}, {Value:"from-recovery"} ] }
                            ]}
                        ]}
                    ]}
```

If the builder had emitted `error.handle` at top level (or shifted the
nested `variable.set` out of the modifier's `Actions` list), the same
test text would still parse and the step would still complete — but
recovery would never run, `%!error%` would surface, and the assertions
would fail. They pass, so the build landed where the text claimed.

## Next

```
VERDICT: PASS
Next: run.ps1 security app-lowercase "Review the code on branch app-lowercase" -b app-lowercase
```
