namespace PLang.Tests.App.SnapshotTests;

public class StaticsAndModesSnapshotTests
{
    [Test]
    public async Task Statics_RoundTrip_PreservesNameValuePairs()
    {
        // App._statics survives Capture/Restore (provisional — flagged in todos.md).
        await Task.CompletedTask;
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task Build_RoundTrip_PreservesIsEnabled()
    {
        // App.Build is a @this with IsEnabled; Capture/Restore round-trips that bool.
        await Task.CompletedTask;
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task Testing_RoundTrip_PreservesIsEnabled()
    {
        // App.Testing is a @this with IsEnabled; Capture/Restore round-trips that bool.
        await Task.CompletedTask;
        Assert.Fail("Not implemented");
    }
}
