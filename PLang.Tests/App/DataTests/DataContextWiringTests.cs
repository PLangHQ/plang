using global::app.modules.callback;

namespace PLang.Tests.App.DataTests;

public class DataContextWiringTests
{
    private sealed class FakeCallback : ICallback
    {
        public global::app.callstack.call.Position? Position => null;
        public byte[] Serialize(global::app.actor.context.@this ctx) => Array.Empty<byte>();
        public Task<Data> Run(global::app.actor.context.@this ctx) => Task.FromResult(Data.Ok(true));
    }

    [Test]
    public async Task Data_Constructor_AcceptsContext_AndStoresPrivately()
    {
        // Per Ingi's Q1: keep the existing settable Context property — Data already has Context
        // wired through Variables.Set, Step/Action/Goal dispatch, Envelope, Clone. The pattern
        // is `new data.@this(...) { Context = ctx }`. Pin that pattern + storage round-trip.
        var app = new global::app.@this(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "plang-test-" + System.Guid.NewGuid().ToString("N")[..8]));
        var data = new Data("v") { Value = 1, Context = app.User.Context };
        await Assert.That(data.Context).IsSameReferenceAs(app.User.Context);
    }

    [Test]
    public async Task Data_LazySignature_ReadsExpiryFromContextAppCallbackSignature()
    {
        // The lazy Signature getter resolves expiry through ctx.App.Callback.Signature.Expires.
        var app = new global::app.@this(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "plang-test-" + System.Guid.NewGuid().ToString("N")[..8]));
        app.Callback.Signature.Expires = TimeSpan.FromSeconds(30);

        var data = new Data("cb") { Value = new FakeCallback(), Context = app.User.Context };
        app.User.Context.Variables.Set(data);

        var sig = data.Signature!;
        var diff = (sig.Expires!.Value - DateTimeOffset.UtcNow).TotalMilliseconds;
        await Assert.That(diff).IsGreaterThan(20_000);
        await Assert.That(diff).IsLessThan(40_000);
    }

    [Test]
    public async Task Data_BareConstructorWithoutContext_NoLongerCompiles_OrThrowsOnSignatureRead()
    {
        // Pins the resolution: a Data constructed without Context throws InvalidOperationException
        // when EnsureSigned() is called. Stage 3 chose option (b) (additive) per Ingi —
        // construction-without-ctx still compiles, but signing requires ctx.
        var data = new Data("v") { Value = 1 }; // no Context
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            data.EnsureSigned();
            await Task.CompletedTask;
        });
    }
}
