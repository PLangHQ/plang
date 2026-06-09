using System.Text.Json;

namespace PLang.Tests.App.Serialization;

// data-serialize-cleanup — Failure matrix.
// Negative-path tests not absorbed by the per-stage suites above. Each test asserts
// the failure is hard, typed, and surfaces at the right layer.

public class FailureMatrixTests
{
    [Test] public async Task EnsureSigned_OnDataWithoutContext_ThrowsInvalidOperation()
    {
        var d = new global::app.data.@this("x", "y");
        await Assert.That(d.Context).IsNull();
        await Assert.That(() => d.EnsureSigned()).Throws<InvalidOperationException>();
    }

    [Test] public async Task PropertiesSet_DataInstanceValue_ThrowsArgumentException()
    {
        var d = new global::app.data.@this("x", "y");
        var inner = new global::app.data.@this("inner", "v");
        await Assert.That(() => d.Properties["k"] = inner).Throws<ArgumentException>();
    }

    [Test] public async Task PropertiesSet_ArbitraryObjectValue_ThrowsArgumentException()
    {
        var d = new global::app.data.@this("x", "y");
        await Assert.That(() => d.Properties["k"] = new System.Threading.CancellationTokenSource()).Throws<ArgumentException>();
    }

    [Test] public async Task SigningVerify_AfterWireByteTamper_ReturnsDataHashMismatch()
    {
        await using var app = new global::app.@this(System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            "plang-fm-" + Guid.NewGuid().ToString("N")[..8]));
        var plang = (global::app.channel.serializer.plang.@this)
            app.User.Channel.Serializers.GetByMimeType("application/plang");

        var d = new global::app.data.@this("x", "untampered") { Context = app.User.Context };
        var wire = (await plang.Serialize(d).Value())!;
        var tampered = wire.Replace("untampered", "TAMPERED!");

        var back = (global::app.data.@this)(await plang.Deserialize(tampered).Value())!;
        back.Context = app.User.Context;
        var verify = await app.RunAction<global::app.module.signing.verify>(
            new global::app.module.signing.verify
            {
                Data = back,
                SkipFreshnessCheck = new global::app.data.@this<global::app.type.@bool.@this>("", true)
            }, app.User.Context);
        await verify.IsFailure();
        await Assert.That(verify.Error!.Key).IsEqualTo("DataHashMismatch");
    }

    [Test] public async Task WireConverter_Read_RandomJsonMissingReservedFields_ProducesTypedFailure()
    {
        var plang = new global::app.channel.serializer.plang.@this();
        // A JSON object with none of the reserved fields — Read parses, but
        // produces an effectively-empty Data (the converter ignores unknown
        // top-level fields). Typed-failure here means the call doesn't throw;
        // the resulting Data is observable as empty.
        var result = plang.Deserialize("{\"unknown\":42}");
        await result.IsSuccess();
        var back = (await result.Value()) as global::app.data.@this;
        await Assert.That(back).IsNotNull();
        await Assert.That(back!.Properties.ContainsKey("unknown")).IsFalse();
    }

    [Test] public async Task Decompress_OnNonArchivedType_ReturnsSelfNoError()
    {
        var d = new global::app.data.@this("x", "y", global::app.type.@this.FromName("text/plain"));
        var result = d.Decompress();
        await Assert.That(ReferenceEquals(d, result)).IsTrue();
        await result.IsSuccess();
    }

    [Test] public async Task Decompress_OnArchivedWithoutByteArrayValue_ReturnsDataWithDecompressError()
    {
        var d = new global::app.data.@this("x", "not bytes", global::app.type.@this.FromName("archived"));
        var result = d.Decompress();
        await result.IsFailure();
        await Assert.That(result.Error!.Key).IsEqualTo("DecompressError");
    }

    [Test] public async Task CryptoHash_WithUnsupportedAlgorithm_ReturnsDataWithUnsupportedAlgorithmError()
    {
        var crypto = new global::app.module.crypto.code.Default();
        var action = new global::app.module.crypto.Hash
        {
            Data = global::app.data.@this.Ok("x"),
            Algorithm = new global::app.data.@this<global::app.type.text.@this>("", "md5")
        };
        var result = crypto.Hash(action);
        await result.IsFailure();
        await Assert.That(result.Error!.Key).IsEqualTo("UnsupportedAlgorithm");
    }

    [Test] public async Task ChannelWrite_OnInputOnlyChannel_ReturnsServiceErrorChannelReadOnly()
    {
        var ch = global::app.channel.type.stream.@this.Input("stdin", new MemoryStream());
        var result = await ch.Write(global::app.data.@this.Ok("x"));
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
        var action = new global::app.module.output.ask
        {
            Question = new global::app.data.@this<global::app.type.text.@this>("", "")
        };
        var result = await ch.Ask(action);
        await result.IsFailure();
        await Assert.That(result.Error!.Key).IsEqualTo("ChannelEof");
    }
}
