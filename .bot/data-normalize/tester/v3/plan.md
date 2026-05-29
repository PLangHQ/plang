# tester v3 — plan

**Context.** No prior tester output on this branch — this is the first tester
pass. Versioning rule says match coder's version, so we're v3.

**Coder/codeanalyzer state.** codeanalyzer v3 PASSED. coder v3 closed V1
(`json.Writer.EndRecord` was hard-coding `View.Out` when normalizing inner
Property values) by threading the writer-construction-time `View` into the
field `_view`, and added one fixture:
`PLang.Tests/App/Serialization/CanonicalizationTests.cs:101`
`StoreView_PropagatesIntoInnerDataProperties_NotHardcodedToOut`.

**Process violation to record.** No `baseline-tests.md` exists at
`.bot/data-normalize/coder/v3/` (nor v1/v2). Coder v3 reports clean results
(C# 3381/3381, PLang 233/233) but I can't separate regressions from
pre-existing failures without that file. I will treat the post-rebuild state
as the ground truth and flag any failures as regressions absent evidence
otherwise. Per `/memory/feedback_strict_red_is_red.md` any red is FAIL —
the absence of a baseline doesn't grant carve-outs.

## Validation strategy

1. **Clean rebuild.** Wipe bin/obj on PlangConsole, PLang, PLang.Tests,
   PLang.Generators per CLAUDE.md stale-binary trap.
2. **C# tests.** `dotnet run --project PLang.Tests`. Record counts.
3. **PLang tests.** `cd Tests && ../PlangConsole/bin/Debug/net10.0/plang --test`.
   Before declaring counts, build one test with `cache=false` to confirm the
   builder works (per `/memory/feedback_validate_builder_before_plang_tests.md`).
4. **Quality analysis on V1 fixture** (the only new test in v3):
   - Read the fixture (done).
   - Read `json.Writer.EndRecord` and `Wire.Write` to confirm the View
     threading is symmetric across both Properties-emission paths
     (outer-Wire's own Properties block + inner-Data EndRecord).
   - **Mutation test:** announce, revert `_view` → `View.Out` in
     `EndRecord`'s `NormalizeValue` call, rerun the fixture, expect the
     Store assertion to fail. Revert.
   - Check whether the fixture's coverage extends to the *other* Properties
     site (Wire.Write line 410) — coder v2 was the first site, but does any
     test pin that direction with a Store-mode walk? If not, that's a
     missing-coverage finding (low severity if it's already exercised
     transitively, major if not).
   - Confirm assertion strength: `Contains` / `DoesNotContain` is a coarse
     check on UTF-8 bytes — sufficient here because the secret literal is
     unique enough that no other path inserts it.
5. **Check broader v1/v2 surface.** Look at what else changed across the
   data-normalize stages (Normalize, IWriter, Reconstruct<T>) and ask: are
   the existing tests strong enough that a subtle bug would fail one? Sample
   2-3 high-value spots, don't audit every test on the branch.

## Outputs

- `/workspace/plang/.bot/data-normalize/tester/v3/plan.md` (this file)
- `/workspace/plang/.bot/data-normalize/tester/v3/coverage.json`
- `/workspace/plang/.bot/data-normalize/tester/v3/verdict.json`
- `/workspace/plang/.bot/data-normalize/test-report.json`
- `/workspace/plang/.bot/data-normalize/tester/summary.md`
