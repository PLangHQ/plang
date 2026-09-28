using app.module.signing;
using app.module.signing.code;

namespace PLang.Tests.App.Serialization;

// plang's own format signs a Data that crosses unsigned. A sign that fails is the write's failure —
// the Data is never written unsigned in its place.
public class PlangSignFailureTests : System.IAsyncDisposable
{
    private readonly global::app.@this app = global::PLang.Tests.TestApp.Create("/tmp/PlangSignFail-" + System.Guid.NewGuid().ToString("N")[..6]);
    public async System.Threading.Tasks.ValueTask DisposeAsync() => await app.DisposeAsync();

    private global::app.actor.context.@this Ctx => app.actor.list.User.Context;

    [Test] public async Task AFailedSign_IsTheWritesError_NothingIsWritten()
    {
        app.Code.Register<ISigning>(new FailingSigning());
        await app.Code.SetDefault<ISigning>("failing").IsSuccess();

        using var ms = new MemoryStream();
        var written = await Ctx.Format("application/plang").Encode(ms, app.Ok("hello"), Ctx);

        await written.IsFailure();
        await Assert.That(written.Error!.Key).IsEqualTo("SigningError");
        await Assert.That(ms.Length).IsEqualTo(0);
    }

    private sealed class FailingSigning : ISigning
    {
        public string Name => "failing";
        public bool IsBuiltIn { get; set; }
        public string? Source { get; set; }
        public (KeyPair? keys, global::app.error.Error? error) GenerateKeyPair() => (null, new global::app.error.ActionError("no keys", "KeyGenerationError", 500));
        public global::app.type.item.binary.@this Sign(global::app.type.item.signature.@this unsigned, global::app.type.item.text.@this privateKey) => throw new global::app.error.AppException("Sign failed", "SigningError", 500);
        public global::app.type.item.@bool.@this Verify(global::app.type.item.signature.@this signature) => throw new global::app.error.AppException("Verify failed", "SignatureInvalid", 400);
        public Task<global::app.data.@this> SignAsync(sign action) => Task.FromResult(global::app.data.@this.FromError(new global::app.error.ActionError("Sign failed", "SigningError", 500)));
        public Task<global::app.data.@this<global::app.type.item.@bool.@this>> VerifyAsync(verify action) => Task.FromResult(global::app.data.@this<global::app.type.item.@bool.@this>.FromError(new global::app.error.ActionError("Verify failed", "SignatureInvalid", 400)));
    }
}
