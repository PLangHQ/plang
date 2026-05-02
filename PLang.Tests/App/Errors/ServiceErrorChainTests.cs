namespace PLang.Tests.App.Errors;

// ServiceError.CallFrames is now typed against the new Call.@this and chain[0]
// IS the failing call (behavior tweak vs old shape — chain captured AFTER Push).
public class ServiceErrorChainTests
{
    [Test]
    public async Task ServiceError_CallFrames_TypedAsReadOnlyListOfCall()
    {
        // Type contract: IReadOnlyList<App.CallStack.Call.@this>.
        await Task.Yield();
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task ServiceError_ChainIndexZero_IsFailingCall()
    {
        // Post-Push snapshot includes self at index [0].
        await Task.Yield();
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task ServiceError_ChainWalksCallerToRoot()
    {
        await Task.Yield();
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task ServiceError_ParamsCarriedFromHandlerSnapshot()
    {
        // App.Run sets ServiceError.Params = handler.SnapshotParams() before returning.
        await Task.Yield();
        Assert.Fail("Not implemented");
    }
}
