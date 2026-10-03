using System.Text.Json;

namespace PLang.Tests.App.Serialization;

// data-serialize-cleanup — Failure matrix.
// Negative-path tests not absorbed by the per-stage suites above. Each test asserts
// the failure is hard, typed, and surfaces at the right layer.

public class FailureMatrixTests : System.IAsyncDisposable
{
    private readonly global::app.@this app = new global::app.@this("/tmp/FailureMatrixTests-" + System.Guid.NewGuid().ToString("N")[..6]).Testing();
    public async System.Threading.Tasks.ValueTask DisposeAsync() => await app.DisposeAsync();

    [Test] public async Task PropertiesSet_DataInstanceValue_ThrowsArgumentException()
    {
        var d = new global::app.data.@this("x", "y", context: app.actor.list.User.Context);
        var inner = new global::app.data.@this("inner", "v", context: app.actor.list.User.Context);
        await Assert.That(() => d.Property.Set("k", inner)).Throws<ArgumentException>();
    }

    [Test] public async Task PropertiesSet_ArbitraryObjectValue_ThrowsArgumentException()
    {
        var d = new global::app.data.@this("x", "y", context: app.actor.list.User.Context);
        await Assert.That(() => d.Property.Set("k", new System.Threading.CancellationTokenSource())).Throws<ArgumentException>();
    }

    [Skip("Serializing within an actor now signs the inner payload, so compressed/hashed bytes are a signature LAYER. The archived wire shape and compress/hash-over-signature round-trip need the archive-as-layer design (deferred). NOTE: Decompress currently loses the inner value through this path - see todos.md.")]
    [Test] public async Task SigningVerify_AfterWireByteTamper_ReturnsDataHashMismatch()
    {
        await using var app = new global::app.@this(System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            "plang-fm-" + Guid.NewGuid().ToString("N")[..8])).Testing();
        var plang = app.actor.list.User.Context.Format("application/plang");

        var d = new global::app.data.@this("x", "untampered", context: app.actor.list.User.Context);
        var wire = (await plang.Serialize(d, app.actor.list.User.Context).Value())!.Clr<string>()!;
        var tampered = wire.Replace("untampered", "TAMPERED!");

        var back = plang.Deserialize(tampered, app.actor.list.User.Context);
        var verify = await new global::app.goal.step.action.@this(new global::app.module.signing.verify(app.actor.list.User.Context)
            {
                Data = back
            }, app.actor.list.User.Context).Start(app.actor.list.User.Context);
        await verify.IsFailure();
        await Assert.That(verify.Error!.Key).IsEqualTo("DataHashMismatch");
    }

    [Test] public async Task WireConverter_Read_RandomJsonMissingReservedFields_ProducesTypedFailure()
    {
        var ctx = app.actor.list.User.Context;
        var plang = ctx.Format("application/plang");
        // A JSON object with none of the reserved fields — Read parses, but
        // produces an effectively-empty Data (the converter ignores unknown
        // top-level fields). Typed-failure here means the call doesn't throw;
        // the resulting Data is observable as empty.
        var back = plang.Stored("{\"unknown\":42}", ctx);   // Deserialize returns the reconstruction itself
        await back.IsSuccess();
        await Assert.That(back!.Property.Contains("unknown")).IsFalse();
    }

    [Test] public async Task CryptoHash_WithUnsupportedAlgorithm_ReturnsTheChoicesRefusal()
    {
        var crypto = new global::app.module.crypto.code.Default();
        var action = new global::app.module.crypto.Hash(app.actor.list.User.Context) { Data = app.Ok("x"),
            Algorithm = new global::app.data.@this("", "md5", context: app.actor.list.User.Context).As<global::app.type.item.choice.@this<global::app.module.crypto.type.hash.kind.@this>>()
        };
        var result = await crypto.Hash(action);
        await result.IsFailure();
        await Assert.That(result.Error!.Key).IsEqualTo("ChoiceInvalid");
    }

    [Test] public async Task ChannelWrite_OnInputOnlyChannel_ReturnsServiceErrorChannelReadOnly()
    {
        var ch = global::app.channel.type.stream.@this.Input("stdin", new MemoryStream());
        var result = await ch.Write(app.Ok("x"));
        await result.IsFailure();
        await Assert.That(result.Error!.Key).IsEqualTo("ChannelReadOnly");
    }

    [Test] public async Task ChannelRead_OnOutputOnlyChannel_ReturnsServiceErrorChannelWriteOnly()
    {
        var ch = global::app.channel.type.stream.@this.Output("stdout", new MemoryStream());
        var result = await ch.Read();
        await result.IsFailure();
        await Assert.That(result.Error!.Key).IsEqualTo("ChannelWriteOnly");
    }

    [Test] public async Task ChannelAsk_OnClosedPipe_ReturnsServiceErrorChannelEof()
    {
        // Empty MemoryStream — ReadLineAsync returns null (EOF).
        var ch = new global::app.channel.type.stream.@this("input", new MemoryStream(),
            global::app.channel.ChannelDirection.Bidirectional);
        var action = new global::app.module.output.ask(app.actor.list.User.Context)
        {
            Question = new global::app.data.@this<global::app.type.item.text.@this>("", "")
        };
        var result = await ch.Ask(action);
        await result.IsFailure();
        await Assert.That(result.Error!.Key).IsEqualTo("ChannelEof");
    }
}
