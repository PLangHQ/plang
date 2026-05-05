namespace PLang.Tests.App.CallStackTests;

public class CallStackSnapshotTests
{
    [Test]
    public async Task CallStack_Capture_WalksActiveFrameChain_OuterToBottom()
    {
        // Captured chain is ordered outer Calls first, throwing Call last (bottom = resume point).
        await Task.CompletedTask;
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task CallStack_Capture_DropsCompletedChildren_AsHistoryNotState()
    {
        // Children of an active Call that already completed are runtime-only audit; not in snapshot.
        await Task.CompletedTask;
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task CallStack_Restore_RebuildsChain_BottomFrameIsResumePoint()
    {
        // After Restore, BottomFrame matches the originally throwing (goal, step, action).
        await Task.CompletedTask;
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task CallStack_BottomFrame_IdentifiesThrowingCall()
    {
        // BottomFrame on a live CallStack is the deepest active frame — the resume entry point.
        await Task.CompletedTask;
        Assert.Fail("Not implemented");
    }
}
