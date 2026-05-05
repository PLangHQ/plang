namespace PLang.Tests.App.CallbackTests;

public class ErrorCallbackTests
{
    [Test]
    public async Task ErrorCallback_RoundTrip_PreservesAppSnapshotSubtree()
    {
        // Single field — Snapshot.@this — survives serialize/deserialize.
        await Task.CompletedTask;
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task ErrorCallback_Position_ReadsAppCallStackBottomFrame()
    {
        // Position is computed: App.CallStack.BottomFrame on the captured snapshot.
        await Task.CompletedTask;
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task ErrorCallback_Run_ConstructsFreshApp_AndDispatchesRestore()
    {
        // Run boots a fresh App.@this then calls freshApp.Restore(App, ctx).
        await Task.CompletedTask;
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task ErrorCallback_Run_LandsAtBottomFrame_AndReExecutesFailedAction()
    {
        // Bind-jump-run: main loop's first tick lands at BottomFrame's (goal, step, action).
        await Task.CompletedTask;
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task ErrorCallback_DispatchByTypedEnvelope_SelectsRightDeserialize()
    {
        // Data<AskCallback> and Data<ErrorCallback> resolve to their own static factories.
        await Task.CompletedTask;
        Assert.Fail("Not implemented");
    }
}
