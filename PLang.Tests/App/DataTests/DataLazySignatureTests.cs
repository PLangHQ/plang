namespace PLang.Tests.App.DataTests;

public class DataLazySignatureTests
{
    [Test]
    public async Task DataSignature_FirstAccess_PopulatesViaSigningSignAsync()
    {
        // First read of data.Signature triggers signing.SignAsync; before that, the
        // backing field is null.
        await Task.CompletedTask;
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task DataSignature_CachedAfterFirstPopulate_ReturnsSameInstance()
    {
        // Subsequent reads return the same Signature object — no re-signing.
        await Task.CompletedTask;
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task DataSignature_Expires_SeededFromAppCallbackConfig_OnlyForICallbackValues()
    {
        // app.Callback.Signature.ExpiresInMs propagates into Data.Signature.Expires
        // when the wrapped value is ICallback.
        await Task.CompletedTask;
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task DataSignature_Expires_NullForNonICallbackValues_EvenWhenConfigSet()
    {
        // Non-callback Data wrapped in application/plang+data still has Expires == null
        // even if the callback config has a timeout — isolation.
        await Task.CompletedTask;
        Assert.Fail("Not implemented");
    }
}
