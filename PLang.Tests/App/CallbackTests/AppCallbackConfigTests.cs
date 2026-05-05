using global::App.Callback;

namespace PLang.Tests.App.CallbackTests;

public class AppCallbackConfigTests
{
    [Test]
    public async Task AppCallback_IsConfigThis_NotAnICallback()
    {
        var app = new global::App.@this("/test");
        // app.Callback is the config @this; assert it does not satisfy ICallback.
        await Assert.That((object)app.Callback).IsNotAssignableTo<ICallback>();
    }

    [Test]
    public async Task AppCallbackSignature_ExpiresInMs_DefaultsToNull()
    {
        var app = new global::App.@this("/test");
        await Assert.That(app.Callback.Signature.ExpiresInMs).IsNull();
    }

    [Test]
    public async Task AppCallbackSignature_AcceptsTimeoutValueInMs()
    {
        var app = new global::App.@this("/test");
        app.Callback.Signature.ExpiresInMs = 300_000;
        await Assert.That(app.Callback.Signature.ExpiresInMs).IsEqualTo(300_000);
    }
}
