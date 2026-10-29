# snapshot — tester summary

**Version:** v6 (the coder's v6), second pass at `18a3b5d51`. Verdict: **FAIL (needs-fixes)**.

## What this is

`snapshot` saves a run's exact state, signs it, keeps it in a `.plang` file or a db row, and resumes it in an app of its own. This pass asks whether the tests would catch a subtly wrong resume, not whether they pass.

## What was done

- First pass (`2df750c73`, `v6/result.md`): suites green and no new red name against runtime2, but two false greens in the resume core (a placeholder test, and mutants that drop or repeat the action after a returned call survived everything) and five behaviours with no plang test.
- Second pass (`v6/result-2.md`): the coder's test-only fixes landed. The two Rest mutants are now red (a new C# test, `FreshAppGoesOn`, `EditedStopPoint`); removing `[Snapshot]` from `call.Event` is red in `EventComesBack` and two C# tests; `start.goal` indexes what exists; four plang tests were added. C# 5641 green; changed-line coverage 1740 of 1908.
- **Open:** `ResumeTakesInTheBinding` is flaky — green alone (15/15), red in 6 of 8 plan-folder runs and 2 of 5 full runs. A failed run leaves `workspace/resumed.marker`, which fails every later run until it is deleted.
- A test is rooted at `test/`: run one with `plang --test path=/plan/...` from `test/`, not from inside its folder.

## Code example (the pattern the fixes use)

```
/ Record: %count% is set by the test before the call, never here, so a Record or Sub run twice counts on to 2
Record
- set %where% = 'workspace/run.plang'
- call Sub city="Reykjavik"
```

## Next

Coder: find why the binding is missing in the resumed sub app when other tests ran (the pending "keep a binding once on resume" may be it) and delete the marker at the top of `ResumeTakesInTheBinding`'s `Start`. Then a last tester run; security follows on pass.
