namespace PLang.Tests.App.CallStackTests;

public class EventsSinceTests
{
    [Test]
    public async Task EventsSince_ReturnsDiffEvents_WithTimestampGreaterThan()
    {
        // Minimum viable surface: 'give me variable mutation events with timestamp > T'.
        await Task.CompletedTask;
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task EventsSince_EmptyWhenNoMutations()
    {
        // No mutations after T → empty enumeration; not null.
        await Task.CompletedTask;
        Assert.Fail("Not implemented");
    }
}
