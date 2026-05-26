# tester — fix-stepvartypes-incremental

**Version:** v3
**Verdict:** FAIL (strict red rule)

## What this is

v2 (re-issued) failed on 21 red PLang tests. Coder shipped four commits addressing the root causes (path canonical form, builder teaching for condition.if, JSON serialization, .pr rebuild). v3 verifies the result and applies the new rules.

## What was done

1. **Validated builder first** (new rule). Created a 4-primitive smoke test in the repo at `Tests/BuilderSanity/` (set, foreach, if, call). Built all four goals with `cache=false` — all succeeded. Smoke test then ran via `plang --test` and **PASSED**. Builder is functional, so `plang --test` results are trustworthy.
2. **C# suite:** 3036/3036 pass, 0 failed.
3. **PLang suite:** 212 pass / 6 fail / 218 total. From v2's 196/21 → +16 passes, −15 failures.
4. The 6 remaining failures are all `*.fixture.goal` files designed to fail (FailsVar asserts 42=99, SensitiveFail asserts %MyIdentity%='will-not-match'). They back the test.report rendering tests. Discovery picks them up despite their intentional-failure role.

## Verdict reasoning

**FAIL on strict-red rule.** All behavioral failures from v2 are closed — that's excellent progress. But 6 PLang tests still show red, and the strict rule says red is red regardless of cause. The fix is either (a) update discovery to skip `*.fixture.goal` files or `_*` folders, (b) move the fixtures outside `Tests/`, or (c) get explicit out-of-scope sign-off.

## Code example — the BuilderSanity smoke test

Added to the repo so future tester runs can validate the builder before trusting test results:

```plang
Start
- set %total% = 0
- set %items% = '[1, 2, 3]'
- foreach %items%, call AddItem item=%item%
- if %total% is greater than 5, call MarkBig
- call Finalize
- assert %total% equals 6
- assert %label% equals 'big-and-done'
```

Helper goals (AddItem, MarkBig, Finalize) each in their own file. Run with:

```bash
cd Tests && plang '--build={"files":["BuilderSanity/BuilderSanity.test.goal","BuilderSanity/AddItem.goal","BuilderSanity/MarkBig.goal","BuilderSanity/Finalize.goal"],"cache":false}'
```

## Process gap (still open)

No `coder/` folder exists. Three coder versions worth of work without a single `coder/v<N>/plan.md`, `summary.md`, or `baseline-tests.md`. Flagging again. Not gating verdict.

## Next

```
run.ps1 coder stepvartypes-incremental "Resolve 6 remaining red tests — they're *.fixture.goal files in _fixtures_*/ folders designed to fail (back the test.report rendering tests). Either update test discovery to skip *.fixture.goal or _*/ folders, or move the fixtures outside Tests/. Files: Modules/Test/Report/_fixtures_{fail,sensitive}/{failsvar,sensitivefail}.fixture.goal and TestModule/Report/_fixtures_{fail,sensitive}/* (the latter is a duplicate path, may also need cleanup)." -b fix-stepvartypes-incremental
```
