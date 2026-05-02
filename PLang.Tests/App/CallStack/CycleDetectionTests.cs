namespace PLang.Tests.App.CallStack;

// MaxDepth + ContainsGoal enforcement on Push.
public class CycleDetectionTests
{
    [Test]
    public async Task Push_ExceedsMaxDepth_ThrowsCallStackOverflowException()
    {
        await Task.Yield();
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task CallStackOverflowException_IncludesChainPath()
    {
        // Exception message / property exposes the chain path so renderers can show it.
        await Task.Yield();
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task Push_DirectGoalCycle_ThrowsViaContainsGoal()
    {
        // Pushing goal A while A is already in the Caller chain throws.
        await Task.Yield();
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task Push_IndirectGoalCycle_Throws()
    {
        // A → B → A trips ContainsGoal.
        await Task.Yield();
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task Push_RepeatedSiblingNotInChain_DoesNotThrow()
    {
        // After A → B pops, A → C is fine — only the live chain matters.
        await Task.Yield();
        Assert.Fail("Not implemented");
    }
}
