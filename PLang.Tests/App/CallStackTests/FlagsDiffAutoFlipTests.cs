namespace PLang.Tests.App.CallStackTests;

public class FlagsDiffAutoFlipTests
{
    [Test]
    public async Task FlagsDiff_AutoFlipsOn_DuringErrorProcessing()
    {
        // Flags.Diff is off by default; flips on for the duration of error dispatch
        // so SnapshotAt(error) can replay events.
        await Task.CompletedTask;
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task FlagsDiff_RestoredToPriorState_AfterErrorPathCompletes()
    {
        // After error processing, Flags.Diff returns to whatever it was before the throw.
        await Task.CompletedTask;
        Assert.Fail("Not implemented");
    }
}
