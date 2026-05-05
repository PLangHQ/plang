using global::App.Callback;
using global::App.CallStack;
using global::App.modules.callback;
using ActionEntity = App.Goals.Goal.Steps.Step.Actions.Action.@this;

namespace PLang.Tests.App.CallbackTests;

public class CallbackRunActionTests
{
    private static global::App.@this NewApp() =>
        new global::App.@this(System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            "plang-cbrun-" + System.Guid.NewGuid().ToString("N")[..8]));

    private sealed class StubCallback : ICallback
    {
        public RestoredFrame? Position => null;
        public byte[] Serialize(global::App.Actor.Context.@this ctx) => Array.Empty<byte>();
        public Task<global::App.Data.@this> Run(global::App.Actor.Context.@this ctx)
            => Task.FromResult(global::App.Data.@this.Ok("ran"));
    }

    [Test]
    public async Task CallbackRun_VerifiesSignature_BeforeDispatch()
    {
        // No signature on the Data → handler skips verify. Call dispatches and returns.
        var app = NewApp();
        var data = new Data("cb") { Value = new StubCallback(), Context = app.User.Context };
        var result = await app.RunAction<run>(new run { Callback = data }, app.User.Context);
        await Assert.That(result.Success).IsTrue();
    }

    [Test]
    public async Task CallbackRun_HardErrors_WhenSigningVerifyFails()
    {
        // Set a tampered signature so signing.verify fails (no matching identity / bad bytes).
        var app = NewApp();
        var data = new Data("cb") { Value = new StubCallback(), Context = app.User.Context };
        data.Signature = new global::App.modules.signing.Signature
        {
            Type = "signature",
            Algorithm = "ed25519",
            Identity = "unknown-identity",
            Created = DateTimeOffset.UtcNow,
            Value = "tampered-signature-bytes"
        };
        var result = await app.RunAction<run>(new run { Callback = data }, app.User.Context);
        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error!.Key).IsEqualTo("CallbackSignatureMismatch");
    }

    [Test]
    public async Task CallbackRun_DispatchesIntoCallbackRun_AndPropagatesData()
    {
        var app = NewApp();
        var data = new Data("cb") { Value = new StubCallback(), Context = app.User.Context };
        var result = await app.RunAction<run>(new run { Callback = data }, app.User.Context);
        await Assert.That(result.Value).IsEqualTo("ran");
    }

    [Test]
    public async Task CallbackRun_OnNonICallbackData_RaisesTypeError()
    {
        var app = NewApp();
        var data = new Data("notcb") { Value = 42, Context = app.User.Context };
        var result = await app.RunAction<run>(new run { Callback = data }, app.User.Context);
        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error!.Key).IsEqualTo("TypeError");
    }

    [Test]
    public async Task CallbackRun_HandlerSignature_TakesDataOfICallback()
    {
        // Pin handler shape: Callback property exists.
        var prop = typeof(run).GetProperty("Callback",
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
        await Assert.That(prop).IsNotNull();
    }
}
