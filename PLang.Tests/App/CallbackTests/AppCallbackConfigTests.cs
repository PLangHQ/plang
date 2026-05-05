namespace PLang.Tests.App.CallbackTests;

public class AppCallbackConfigTests
{
    [Test]
    public async Task AppCallback_IsConfigThis_NotAnICallback()
    {
        // app.Callback is a config @this, not an ICallback instance.
        await Task.CompletedTask;
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task AppCallbackSignature_ExpiresInMs_DefaultsToNull()
    {
        // Default = no expiry (integrity, not validity).
        await Task.CompletedTask;
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task AppCallbackSignature_AcceptsTimeoutValueInMs()
    {
        // Setting via PLang `- set callback timeout to 5 minutes` writes 300000.
        await Task.CompletedTask;
        Assert.Fail("Not implemented");
    }
}
