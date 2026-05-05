namespace PLang.Tests.App.CallbackTests;

public class ICallbackPositionTests
{
    [Test]
    public async Task ICallback_Position_ReturnsCallFrame_OnAskCallback()
    {
        // AskCallback.Position is its own field — a single Call.@this frame.
        await Task.CompletedTask;
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task ICallback_Position_ReturnsBottomFrame_OnErrorCallback()
    {
        // ErrorCallback.Position is App.CallStack.BottomFrame — read-through, not stored.
        await Task.CompletedTask;
        Assert.Fail("Not implemented");
    }
}
