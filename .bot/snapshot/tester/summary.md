# snapshot — tester summary

**Version:** v6 (matches the coder's v6). Tip reviewed `2df750c73`. Verdict: **FAIL (needs-fixes)**.

## What this is

`snapshot` saves a run's exact state (variables, call stack, bindings, the running event, added properties), signs it, keeps it in a `.plang` file or a db row, and resumes it later in an app of its own. This pass asks whether the tests would catch a subtly wrong resume, not whether they pass.

## What was done

- Suites: C# 5641 (0 red, 45 skipped) on snapshot, against runtime2's 5561 with 6 red (the `SnapshotWireTests` the branch fixes). plang 307/308 (1 skipped, 0 stale), against runtime2's 305/309 with 3 stale. No new red name. No `coder/baseline-failures.txt` exists (process violation).
- Each `.test.goal` under `test/plan/snapshot/` passes alone and in its folder, twice. A test is rooted at `test/`: run it with `plang --test path=/plan/...` from `test/`, not from inside its own folder.
- Behaviours mapped to tests (full table in `v6/result.md`): all seven have a C# test; only "save and resume" and "the error's callback" have a plang one.
- Mutation checks on `PLang/app/call/action/this.cs` `Rest` (reverted): dropping, or repeating, the action after a returned call survives every test; skipping the stopped action's re-run goes red in 26 C# tests and 2 plang tests.
- C# coverage of the diff: 1737 of 1908 changed executable lines reached; unreached list in `v6/result.md` and `v6/coverage.json`.

## Code example (the false green)

```csharp
// SnapshotResumeTests.cs:116 — the only test named for the rule, a placeholder
[Test] public async Task ResumeChain_MultiActionStep_ContinuesAtActionIndexPlusOne()
{
    await Assert.That(true).IsTrue();
}
```

## Next

The coder: (1) a C# test for a multi-action step resumed through a call (the action after the call runs once, the call does not run again), deleting the placeholder; (2) a non-idempotent `FreshAppGoesOn` (file appends, exactly one line each); (3) plang tests for `%!event%` across a save and for edit → sign → resume; (4) a truthful `test/plan/snapshot/start.goal` and `start.md`. Architect: confirm whether `snapshot.sign` (decision 72) is stage 11, still to build. Then a tester re-run; security follows on pass.
