using global::App.Channels.Serializers;
using global::App.Channels.Serializers.Serializer;
using global::App.Callback;

namespace PLang.Tests.App.Serializers;

public class PlangDataSerializerRoundTripTests
{
    private sealed class FakeCallback : ICallback
    {
        public global::App.CallStack.Call.Position? Position => null;
        public byte[] Serialize(global::App.Actor.Context.@this ctx) => Array.Empty<byte>();
        public Task<Data> Run(global::App.Actor.Context.@this ctx) => Task.FromResult(Data.Ok(true));
    }

    [Test]
    public async Task PlangDataSerializer_Write_EmitsTypePlusValuePlusSignature()
    {
        // application/plang+data wire shape is the full envelope: Type + Value + Signature.
        var app = new global::App.@this(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "plang-test-" + System.Guid.NewGuid().ToString("N")[..8]));
        var data = new Data("v") { Value = "hello", Context = app.User.Context };
        app.User.Context.Variables.Set(data);

        var s = app.User.Channels.Serializers.GetByMimeType("application/plang+data");
        var wire = s.Serialize(data);

        await Assert.That(wire.Contains("\"type\"")).IsTrue();
        await Assert.That(wire.Contains("\"value\"")).IsTrue();
        await Assert.That(wire.Contains("\"signature\"")).IsTrue();
    }

    [Test]
    public async Task PlangDataSerializer_Write_TriggersLazySigning_OnFirstSignatureRead()
    {
        // Write reads data.Signature → first access populates via signing.SignAsync.
        var app = new global::App.@this(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "plang-test-" + System.Guid.NewGuid().ToString("N")[..8]));
        var data = new Data("v") { Value = "hello", Context = app.User.Context };
        app.User.Context.Variables.Set(data);

        await Assert.That(data.RawSignature).IsNull();

        var s = app.User.Channels.Serializers.GetByMimeType("application/plang+data");
        s.Serialize(data);

        await Assert.That(data.RawSignature).IsNotNull();
    }

    [Test]
    public async Task PlangDataSerializer_RoundTrip_SignaturePopulatedUnverifiedOnRead()
    {
        var app = new global::App.@this(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "plang-test-" + System.Guid.NewGuid().ToString("N")[..8]));
        var data = new Data("v") { Value = "hello", Context = app.User.Context };
        app.User.Context.Variables.Set(data);

        var s = app.User.Channels.Serializers.GetByMimeType("application/plang+data");
        var wire = s.Serialize(data);
        var restored = s.Deserialize<Data>(wire);

        await Assert.That(restored).IsNotNull();
        await Assert.That(restored!.RawSignature).IsNotNull();
    }

    [Test]
    public async Task PlangDataSerializer_RoundTrip_DoesNotAutoVerify()
    {
        // Reading does NOT auto-verify; verification is the consumer's explicit step.
        // The reconstructed Data has signature populated but unchecked. This is a
        // structural pin — Read returns a Data whose RawSignature is set; nothing
        // calls Verify implicitly.
        var app = new global::App.@this(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "plang-test-" + System.Guid.NewGuid().ToString("N")[..8]));
        var data = new Data("v") { Value = "hello", Context = app.User.Context };
        app.User.Context.Variables.Set(data);

        var s = app.User.Channels.Serializers.GetByMimeType("application/plang+data");
        var wire = s.Serialize(data);
        var restored = s.Deserialize<Data>(wire);

        // Signature present, no verification performed: there's no Verified flag set.
        await Assert.That(restored!.RawSignature).IsNotNull();
        // The signature on the restored Data hasn't been checked — nothing on Data
        // tracks "verified" today, so we assert presence + that round-trip succeeded.
        await Assert.That(restored.Value as string).IsEqualTo("hello");
    }

    [Test]
    public async Task PlangDataSerializer_HandlesApplicationPlangDataMimeType()
    {
        var app = new global::App.@this(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "plang-test-" + System.Guid.NewGuid().ToString("N")[..8]));
        var s = app.User.Channels.Serializers.GetByMimeType("application/plang+data");
        await Assert.That(s).IsTypeOf<global::App.Channels.Serializers.Serializer.Plang.Data>();
        await Assert.That(s.ContentType).IsEqualTo("application/plang+data");
    }
}
