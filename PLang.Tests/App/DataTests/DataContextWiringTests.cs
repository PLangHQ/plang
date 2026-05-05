namespace PLang.Tests.App.DataTests;

public class DataContextWiringTests
{
    [Test]
    public async Task Data_Constructor_AcceptsContext_AndStoresPrivately()
    {
        // Data(value, ctx) is the canonical constructor (Stage 3 architectural decision).
        await Task.CompletedTask;
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task Data_LazySignature_ReadsExpiryFromContextAppCallbackSignature()
    {
        // The lazy Signature getter resolves expiry through ctx.App.Callback.Signature.ExpiresInMs.
        await Task.CompletedTask;
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task Data_BareConstructorWithoutContext_NoLongerCompiles_OrThrowsOnSignatureRead()
    {
        // Pins option (a): no context-less construction at the type level. If a fallback
        // constructor exists for legacy code, reading Signature on it must hard-error.
        await Task.CompletedTask;
        Assert.Fail("Not implemented");
    }
}
