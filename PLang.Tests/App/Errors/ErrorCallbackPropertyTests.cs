namespace PLang.Tests.App.Errors;

public class ErrorCallbackPropertyTests
{
    [Test]
    public async Task ErrorCallback_Property_TriggersAppSnapshot_OnFirstRead()
    {
        // Reading Error.@this.Callback for the first time invokes app.Snapshot()
        // (which includes Variables.SnapshotAt(error) for throw-time view).
        await Task.CompletedTask;
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task ErrorCallback_Property_ReturnsDataOfErrorCallback()
    {
        // The property type is Data<ErrorCallback>. Signature backing field stays null
        // until a serializer reads it.
        await Task.CompletedTask;
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task ErrorCallback_Property_ReadTwice_ReturnsSameDataInstance()
    {
        // Idempotent caching — same Data instance returned on subsequent reads
        // (reference equality).
        await Task.CompletedTask;
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task ErrorCallback_Property_ReturnsTwoIndependentCalls_ForTwoErrors()
    {
        // Two distinct Error instances → two distinct Data<ErrorCallback> instances.
        await Task.CompletedTask;
        Assert.Fail("Not implemented");
    }
}
