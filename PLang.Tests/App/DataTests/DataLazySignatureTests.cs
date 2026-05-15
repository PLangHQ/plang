using global::app.Callback;

namespace PLang.Tests.App.DataTests;

public class DataLazySignatureTests
{
    private sealed class FakeCallback : ICallback
    {
        public global::app.callstack.call.Position? Position => null;
        public byte[] Serialize(global::app.actor.context.@this ctx) => Array.Empty<byte>();
        public Task<Data> Run(global::app.actor.context.@this ctx) => Task.FromResult(Data.Ok(true));
    }

    [Test]
    public async Task DataSignature_FirstAccess_PopulatesViaSigningSignAsync()
    {
        // First read of data.Signature triggers signing.SignAsync; before that, the
        // backing field is null. Verified via RawSignature (no auto-populate) before access.
        var app = new global::app.@this(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "plang-test-" + System.Guid.NewGuid().ToString("N")[..8]));
        var data = new Data("cb") { Value = new FakeCallback(), Context = app.User.Context };
        app.User.Context.Variables.Set(data);

        // Backing field is null before first access.
        await Assert.That(data.RawSignature).IsNull();

        var sig = data.Signature;
        await Assert.That(sig).IsNotNull();
        await Assert.That(data.RawSignature).IsNotNull();
    }

    [Test]
    public async Task DataSignature_CachedAfterFirstPopulate_ReturnsSameInstance()
    {
        var app = new global::app.@this(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "plang-test-" + System.Guid.NewGuid().ToString("N")[..8]));
        var data = new Data("cb") { Value = new FakeCallback(), Context = app.User.Context };
        app.User.Context.Variables.Set(data);

        var first = data.Signature;
        var second = data.Signature;
        await Assert.That(first).IsSameReferenceAs(second);
    }

    [Test]
    public async Task DataSignature_Expires_SeededFromAppCallbackConfig_OnlyForICallbackValues()
    {
        // app.Callback.Signature.Expires propagates into Data.Signature.Expires
        // when the wrapped value is ICallback.
        var app = new global::app.@this(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "plang-test-" + System.Guid.NewGuid().ToString("N")[..8]));
        app.Callback.Signature.Expires = TimeSpan.FromMinutes(1);

        var data = new Data("cb") { Value = new FakeCallback(), Context = app.User.Context };
        app.User.Context.Variables.Set(data);

        var sig = data.Signature;
        await Assert.That(sig).IsNotNull();
        await Assert.That(sig!.Expires).IsNotNull();
        // Roughly ~now + 60s.
        var diff = (sig.Expires!.Value - DateTimeOffset.UtcNow).TotalMilliseconds;
        await Assert.That(diff).IsGreaterThan(50_000);
        await Assert.That(diff).IsLessThan(70_000);
    }

    [Test]
    public async Task DataSignature_Expires_NullForNonICallbackValues_EvenWhenConfigSet()
    {
        var app = new global::app.@this(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "plang-test-" + System.Guid.NewGuid().ToString("N")[..8]));
        app.Callback.Signature.Expires = TimeSpan.FromMinutes(1);

        var data = new Data("payload") { Value = "hello", Context = app.User.Context };
        app.User.Context.Variables.Set(data);

        // Non-ICallback values do NOT auto-populate Signature on read; trigger explicitly.
        data.EnsureSigned();
        await Assert.That(data.RawSignature).IsNotNull();
        await Assert.That(data.RawSignature!.Expires).IsNull();
    }
}
