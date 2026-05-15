using global::app.Callback;

namespace PLang.Tests.App.CallbackTests;

public class AppCallbackConfigTests
{
    [Test]
    public async Task AppCallback_IsConfigThis_NotAnICallback()
    {
        var app = new global::app.@this("/test");
        // app.Callback is the config @this; assert it does not satisfy ICallback.
        await Assert.That((object)app.Callback).IsNotAssignableTo<ICallback>();
    }

    [Test]
    public async Task AppCallbackSignature_Expires_DefaultsToNull()
    {
        var app = new global::app.@this("/test");
        await Assert.That(app.Callback.Signature.Expires).IsNull();
    }

    [Test]
    public async Task AppCallbackSignature_AcceptsTimeoutValueInMs()
    {
        var app = new global::app.@this("/test");
        app.Callback.Signature.Expires = TimeSpan.FromMinutes(5);
        await Assert.That(app.Callback.Signature.Expires).IsEqualTo(TimeSpan.FromMinutes(5));
    }
}
