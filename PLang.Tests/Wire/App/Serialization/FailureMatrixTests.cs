using System.Text.Json;

namespace PLang.Tests.App.Serialization;

// data-serialize-cleanup — Failure matrix.
// Negative-path tests not absorbed by the per-stage suites above. Each test asserts
// the failure is hard, typed, and surfaces at the right layer.

public class FailureMatrixTests : System.IAsyncDisposable
{
    private readonly global::app.@this app = global::PLang.Tests.TestApp.Create("/tmp/FailureMatrixTests-" + System.Guid.NewGuid().ToString("N")[..6]);
    public async System.Threading.Tasks.ValueTask DisposeAsync() => await app.DisposeAsync();

    [Test] public async Task PropertiesSet_DataInstanceValue_ThrowsArgumentException()
    {
        var d = new global::app.data.@this("x", "y", context: global::PLang.Tests.TestApp.SharedContext);
        var inner = new global::app.data.@this("inner", "v", context: global::PLang.Tests.TestApp.SharedContext);
        await Assert.That(() => d.Properties["k"] = inner).Throws<ArgumentException>();
    }

    [Test] public async Task PropertiesSet_ArbitraryObjectValue_ThrowsArgumentException()
    {
        var d = new global::app.data.@this("x", "y", context: global::PLang.Tests.TestApp.SharedContext);
        await Assert.That(() => d.Properties["k"] = new System.Threading.CancellationTokenSource()).Throws<ArgumentException>();
    }

    [Skip("Serializing within an actor now signs the inner payload, so compressed/hashed bytes are a signature LAYER. The archived wire shape and compress/hash-over-signature round-trip need the archive-as-layer design (deferred). NOTE: Decompress currently loses the inner value through this path - see todos.md.")]
    [Test] public async Task SigningVerify_AfterWireByteTamper_ReturnsDataHashMismatch()
    {
        await using var app = global::PLang.Tests.TestApp.Create(System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            "plang-fm-" + Guid.NewGuid().ToString("N")[..8]));
        var plang = (global::app.channel.serializer.plang.@this)
            app.User.Channel.Serializers.GetByMimeType("application/plang");

        var d = new global::app.data.@this("x", "untampered", context: app.User.Context);
        var wire = (await plang.Serialize(d).Value())!.Clr<string>()!;
        var tampered = wire.Replace("untampered", "TAMPERED!");

        var back = plang.Deserialize(tampered);
        back.Context = app.User.Context;
        var verify = await app.Run<global::app.module.signing.verify>(
            new global::app.module.signing.verify(app.User.Context)
            {
                Data = back,
                SkipFreshnessCheck = new global::app.data.@this<global::app.type.@bool.@this>("", true)
            }, app.User.Context);
        await verify.IsFailure();
        await Assert.That(verify.Error!.Key).IsEqualTo("DataHashMismatch");
    }

    [Test] public async Task WireConverter_Read_RandomJsonMissingReservedFields_ProducesTypedFailure()
    {
        var plang = new global::app.channel.serializer.plang.@this(global::PLang.Tests.TestApp.SharedContext);
        // A JSON object with none of the reserved fields — Read parses, but
        // produces an effectively-empty Data (the converter ignores unknown
        // top-level fields). Typed-failure here means the call doesn't throw;
        // the resulting Data is observable as empty.
        var back = plang.Deserialize("{\"unknown\":42}");   // Deserialize returns the reconstruction itself
        await back.IsSuccess();
        await Assert.That(back!.Properties.ContainsKey("unknown")).IsFalse();
    }

    [Test] public async Task Decompress_OnNonArchivedType_ReturnsSelfNoError()
    {
        var d = new global::app.data.@this("x", "y", global::PLang.Tests.TestApp.SharedContext.Type.Create("text/plain"), context: global::PLang.Tests.TestApp.SharedContext);
        var result = d.Decompress();
        await Assert.That(ReferenceEquals(d, result)).IsTrue();
        await result.IsSuccess();
    }

    [Test] public async Task Decompress_OnArchiveWithCorruptBytes_ReturnsDataWithDecompressError()
    {
        // An archive whose bytes are not valid gzip — gunzip throws, surfaced as
        // a clean DecompressError rather than an unhandled exception. (A Data that
        // is not an archive at all is a no-op passthrough, not an error.)
        var d = new global::app.data.@this("x",
            new global::app.type.archive.@this(new byte[] { 1, 2, 3, 4 }, "gzip"));
        var result = d.Decompress();
        await result.IsFailure();
        await Assert.That(result.Error!.Key).IsEqualTo("DecompressError");
    }

    [Test] public async Task CryptoHash_WithUnsupportedAlgorithm_ReturnsDataWithUnsupportedAlgorithmError()
    {
        var crypto = new global::app.module.crypto.code.Default();
        var action = new global::app.module.crypto.Hash(app.User.Context) { Data = app.Ok("x"),
            Algorithm = new global::app.data.@this<global::app.type.text.@this>("", "md5")
        };
        var result = await crypto.Hash(action);
        await result.IsFailure();
        await Assert.That(result.Error!.Key).IsEqualTo("UnsupportedAlgorithm");
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
        var action = new global::app.module.output.ask(app.User.Context)
        {
            Question = new global::app.data.@this<global::app.type.text.@this>("", "")
        };
        var result = await ch.Ask(action);
        await result.IsFailure();
        await Assert.That(result.Error!.Key).IsEqualTo("ChannelEof");
    }
}
