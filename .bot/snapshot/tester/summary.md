# snapshot — tester summary

**Version:** v6, third pass at `c1e1a9248`. Verdict: **PASS**.

## What this is
`snapshot` saves a run's state, signs it, keeps it in a `.plang` file or db row, and resumes it in an app of its own. The tester asks whether the tests would catch a subtly wrong resume.

## What was done
- Pass 1 (`v6/result.md`): FAIL — placeholder test, surviving Rest mutants, missing plang tests, stale index.
- Pass 2 (`v6/result-2.md`): FAIL — fixes in, but `ResumeTakesInTheBinding` flaky.
- Pass 3 (`v6/result-3.md`): PASS — identity race fixed; C# 5647 green; plang 311/312 (1 skipped) in 10 of 10 runs; Rest re-run, rest-dropped and `Event` `[Snapshot]` mutants all red.
- A test is rooted at `test/`: run one with `plang --test path=/plan/...` from `test/`.

## Next
Security.
