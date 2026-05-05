namespace PLang.Tests.App.Errors;

public class ErrorsTrailSnapshotTests
{
    [Test]
    public async Task ErrorsTrail_RoundTrip_PreservesEntries()
    {
        // Populate Trail with a sequence of errors; Capture; Restore; assert deep equality.
        await Task.CompletedTask;
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task ErrorsTrail_AfterRestore_IsReadOnly()
    {
        // The restored Trail rejects mutation — historic record, not a live append target.
        await Task.CompletedTask;
        Assert.Fail("Not implemented");
    }
}
