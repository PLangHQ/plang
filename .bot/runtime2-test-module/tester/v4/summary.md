# Tester v4 — Summary

## What this is

Re-review of the coder's v2 session (commit `730bfce0`), which addressed my v3 test-quality findings. The v3 verdict was `needs-fixes` with 17 findings (4 critical, 7 major, 6 minor). The coder's v2 claim: all 17 addressed, +24 new tests, 0 regressions, same pre-existing LLM flake.

My job: validate with fresh eyes. Especially: watch for the highest-risk class of bug — review-driven fixes that follow the letter of the reviewer's ask but dodge the spirit (add the assertion shape the reviewer requested, but route around the actual code path).

## What was done

Ran the full C# test suite, regenerated coverage via coverlet (dotnet-coverage wouldn't attach on this WSL due to missing libxml2), and read every new or modified test applying the deletion test: "if I deleted the core assertion block, would anything fail?"

**Result:** 15 of 17 v3 findings fully discharged, 2 partially (both the PLang `.test.goal` integration pipeline — rename done, but no `.pr` on disk because the in-session coder couldn't drive the LLM builder).

### Five new findings (all minor/major, none critical)

- **F1 (major):** 16 of 19 `.test.goal` files still contain `throw "not implemented"` — they'll Fail post-build, not Skip. Zero PLang E2E integration tests run green today.
- **F2 (major):** The production coverage subscriber at `run.cs:96-116` (reading `result.Properties["branchLabel"]` / `["branchChain"]` and calling the Coverage recorders) stays at 0% coverage. `OrchestrateBranchCoverageTests` replicates the filter shape in a look-alike subscriber but never routes through `test.run`. A typo in the production Properties keys would ship silently.
- **F3 (minor):** `OrchestrateBranchCoverageTests` has one non-discriminating assertion — `subran == 1` would pass in both the bug case and the fix case (DisableChildrenOf-silently-skipped still lets the sub-step run).
- **F4 (minor):** `Executor.Run(args)` itself is 0% — only `Configure(args)` is tested. The composition is untested.
- **F5 (minor):** `RunSingleAsync`'s exception-path catches (lines 139-147) are 0%.

### Suite + coverage

- 2268 total / 2267 pass / 1 pre-existing LLM flake — matches coder's claim.
- PLang suite: nothing runnable — all 19 `.test.goal` files are Stale.
- Coverage deltas on the files the coder touched are large: Executor 0% → 87.3%, Coverage.cs 65.5% → 100%, Debug 2.0% → 50.3%, SplitAtConditions path fully covered.

## Code example — shape of a v2 fix that passed the deletion test

Before (v1, tautology — what I flagged in v3):

```csharp
public async Task Run_TestingIsEnabled_SetToTrueInChildApp()
{
    var test = BuildFixture(...);
    var results = await RunTests(new List<TestFile> { test });
    await Assert.That(results.Single().Status).IsEqualTo(TestStatus.Pass);
    // Nothing here actually observes childApp.Testing.IsEnabled.
}
```

After (v2, probe via new `ChildAppCreated` hook):

```csharp
public async Task Run_TestingIsEnabled_SetToTrueInChildApp()
{
    bool? observed = null;
    void Probe(App.@this childApp)
    {
        if (childApp.AbsolutePath.StartsWith(_tempDir))
            observed = childApp.Testing.IsEnabled;
    }
    App.modules.test.run.ChildAppCreated += Probe;
    try
    {
        await RunTests(...);
        await Assert.That(observed).IsEqualTo(true);
    }
    finally { App.modules.test.run.ChildAppCreated -= Probe; }
}
```

Deleting `childApp.Testing.IsEnabled = true;` from `run.cs` now flips `observed` to `false` → test fails. Genuine guard, not a tautology.

Same pattern applied to `SystemDirectory_Inherited`, `ParallelExecution` (counter depth), and `OnlyReadyTests` (child-app-count probe). Each now discriminates.

## Verdict

**pass** — recommend **security** analyst next.

Residual F1–F5 are real but small and don't block shipping. The suite is in materially better shape than v1 ever was.
