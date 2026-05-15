using global::app.Callback;
using global::app.CallStack;
using global::app.modules.callback;
using ActionEntity = app.Goals.Goal.Steps.Step.Actions.Action.@this;

namespace PLang.Tests.App.CallbackTests;

public class CallbackRunActionTests
{
    private static global::app.@this NewApp() =>
        new global::app.@this(System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            "plang-cbrun-" + System.Guid.NewGuid().ToString("N")[..8]));

    private sealed class StubCallback : ICallback
    {
        public global::app.CallStack.Call.Position? Position => null;
        public byte[] Serialize(global::app.Actor.Context.@this ctx) => Array.Empty<byte>();
        public Task<global::app.data.@this> Run(global::app.Actor.Context.@this ctx)
            => Task.FromResult(global::app.data.@this.Ok("ran"));
    }

    [Test]
    public async Task CallbackRun_VerifiesSignature_BeforeDispatch()
    {
        // No signature pre-set; in-process Data has Context, so EnsureSigned signs locally,
        // signing.verify roundtrips against the same identity, dispatch runs, returns Success.
        // (Pre-S-F1 fix this path skipped verify entirely; post-fix it's a real verify.)
        var app = NewApp();
        var data = new Data("cb") { Value = new StubCallback(), Context = app.User.Context };
        var result = await app.RunAction<run>(new run { Callback = data }, app.User.Context);
        await Assert.That(result.Success).IsTrue();
    }

    [Test]
    public async Task CallbackRun_RejectsUnsignableData_WhenContextMissing()
    {
        // Wire-deserialized Data with no signature AND no Context can't be sealed by
        // EnsureSigned. Handler must reject with MissingCallbackSignature — this is the
        // S-F1 hard gate: absence-of-signature on a Data we can't sign locally is rejection,
        // never trust.
        var app = NewApp();
        var data = new Data("cb") { Value = new StubCallback() }; // NOTE: no Context
        var result = await app.RunAction<run>(new run { Callback = data }, app.User.Context);
        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error!.Key).IsEqualTo("MissingCallbackSignature");
    }

    [Test]
    public async Task CallbackRun_HardErrors_WhenSigningVerifyFails()
    {
        // Set a tampered signature so signing.verify fails (no matching identity / bad bytes).
        var app = NewApp();
        var data = new Data("cb") { Value = new StubCallback(), Context = app.User.Context };
        data.Signature = new global::app.modules.signing.Signature
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
