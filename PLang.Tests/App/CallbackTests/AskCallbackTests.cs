namespace PLang.Tests.App.CallbackTests;

public class AskCallbackTests
{
    [Test]
    public async Task AskCallback_RoundTrip_PreservesPositionActorAndVariables()
    {
        // Three explicit fields survive a serialize/deserialize cycle.
        await Task.CompletedTask;
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task AskCallback_Serialize_CallsCryptoEncrypt_AndReturnsEncryptedBytes()
    {
        // Serialize pipes payload through ctx.App.Modules.Get("crypto").EncryptAsync.
        await Task.CompletedTask;
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task AskCallback_Deserialize_CallsCryptoDecrypt_AndReconstructsRecord()
    {
        // Static factory: Deserialize(bytes, ctx) reverses Serialize end-to-end.
        await Task.CompletedTask;
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task AskCallback_Run_BindsVariables_AndDispatchesAskActionWithBoundValue()
    {
        // Run binds Variables into the resumed App and dispatches the ask at Position
        // — the action returns the bound value rather than issuing a fresh ask.
        await Task.CompletedTask;
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task AskCallback_Run_ReturnsResumedActionResult_AsTaskOfData()
    {
        // Run's return is Task<Data>; the resumed action's value flows back so the caller
        // can chain on it.
        await Task.CompletedTask;
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task AskCallback_Run_HardErrors_OnGoalStubNotFound()
    {
        // Position.Goal stub doesn't resolve in the resumed App's registry → referent-integrity error.
        await Task.CompletedTask;
        Assert.Fail("Not implemented");
    }
}
