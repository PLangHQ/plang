namespace PLang.Tests.App.VariablesTests;

public class SnapshotAtErrorTests
{
    [Test]
    public async Task SnapshotAt_ReturnsVariablesProjection_AtThrowTime()
    {
        // app.Variables.SnapshotAt(error) returns a Variables.@this — projection,
        // not a flat dict.
        await Task.CompletedTask;
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task SnapshotAt_ConsultsCallStackEventsSince_AndReverseApplies()
    {
        // Variables asks CallStack for events-since-throwTime and reverse-applies
        // each Set event to current state.
        await Task.CompletedTask;
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task SnapshotAt_ExcludesPostErrorMutationsByHandler()
    {
        // set %x%=1, throw, handler does set %x%=2 — SnapshotAt(error)["x"] == 1.
        await Task.CompletedTask;
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task SnapshotAt_NoMutations_ReturnsCurrentState()
    {
        // No diff events after T → projection equals current state.
        await Task.CompletedTask;
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task SnapshotAt_IsPure_SameInputsSameResult()
    {
        // Idempotent: same (error, current state) always yields equal projection.
        await Task.CompletedTask;
        Assert.Fail("Not implemented");
    }
}
