namespace PLang.Tests.App.CallStackTests;

public class CallSnapshotTests
{
    [Test]
    public async Task Call_Capture_EmitsGoalStub_PrPathPlusHash_NotFullGoal()
    {
        // Wire shape is { PrPath, Hash } only — Goal stays pure with no two-mode serialisation.
        await Task.CompletedTask;
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task Call_Capture_IncludesStepIndexAndActionIndex()
    {
        // Capture emits (Goal-stub, StepIndex, ActionIndex) — the positional triple.
        await Task.CompletedTask;
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task Call_Restore_ResolvesGoalStubAgainstLiveRegistry()
    {
        // Call.Restore looks up the goal by PrPath in the live App.Goals registry.
        await Task.CompletedTask;
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task Call_Restore_HardErrors_OnGoalNotFound()
    {
        // Goal file moved/deleted between issue and resume → referent-integrity error.
        await Task.CompletedTask;
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task Call_Restore_HardErrors_OnHashMismatch_RaisesCallbackGoalHashMismatch()
    {
        // Goal redeployed with different prose → live.Hash != stub.Hash → typed error.
        await Task.CompletedTask;
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task Call_Restore_DoesNotMutateLiveGoal()
    {
        // The Restore path is read-only on App.Goals — pulls a stub through, doesn't touch state.
        await Task.CompletedTask;
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task Call_Restore_HashErrorIsTypedNotBoolean()
    {
        // No 'did the resume succeed?' boolean bubbles up — the type system pins it.
        await Task.CompletedTask;
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task Call_Capture_OmitsTimingTier_AndInFlightNetworkState()
    {
        // Drop bucket: timing tier and in-flight network state never reach the snapshot.
        await Task.CompletedTask;
        Assert.Fail("Not implemented");
    }
}
