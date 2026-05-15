using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using app;
using global::app.Channels.Serializers;
using global::app.Variables;
using global::app.modules.signing;

namespace PLang.Tests.App.Serializers;

/// <summary>
/// Tests global::app.Channels.Serializers.Filters.Transport — re-includes [JsonIgnore] properties
/// that have [In] or [Out] attributes for application/plang transport.
/// </summary>
public class TransportPropertyFilterTests
{
    private static readonly JsonSerializerOptions _defaultOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private static readonly JsonSerializerOptions _inboundOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        TypeInfoResolver = new DefaultJsonTypeInfoResolver
        {
            Modifiers = { global::app.Channels.Serializers.Filters.Transport.ForInbound }
        }
    };

    private static readonly JsonSerializerOptions _outboundOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        TypeInfoResolver = new DefaultJsonTypeInfoResolver
        {
            Modifiers = { global::app.Channels.Serializers.Filters.Transport.ForOutbound }
        }
    };

    [Test]
    public async Task DefaultJson_ExcludesSignature_BecauseJsonIgnore()
    {
        var data = Data.Ok("test-value");
        data.Signature = new Signature
        {
            Identity = "test-key",
            Nonce = "abc123"
        };

        var json = JsonSerializer.Serialize(data, _defaultOptions);

        await Assert.That(json).DoesNotContain("test-key");
        await Assert.That(json).DoesNotContain("abc123");
        await Assert.That(json).DoesNotContain("signature");
    }

    [Test]
    public async Task ForInbound_DeserializesSignature_DespiteJsonIgnore()
    {
        // Simulate wire data with signature included
        var json = """{"name":"","value":"hello","signature":{"type":"signature","algorithm":"ed25519","nonce":"n1","identity":"pubkey1","signature":"sig1"}}""";

        var data = JsonSerializer.Deserialize<Data>(json, _inboundOptions);

        await Assert.That(data).IsNotNull();
        await Assert.That(data!.Signature).IsNotNull();
        await Assert.That(data.Signature!.Identity).IsEqualTo("pubkey1");
        await Assert.That(data.Signature!.Nonce).IsEqualTo("n1");
    }

    [Test]
    public async Task ForInbound_NullSignature_DeserializesWithoutSignature()
    {
        var json = """{"name":"","value":"no-sig"}""";

        var data = JsonSerializer.Deserialize<Data>(json, _inboundOptions);

        await Assert.That(data).IsNotNull();
        await Assert.That(data!.Signature).IsNull();
    }

    [Test]
    public async Task ForOutbound_SerializesSignature_DespiteJsonIgnore()
    {
        var data = Data.Ok("outbound-test");
        data.Signature = new Signature
        {
            Identity = "out-key",
            Nonce = "out-nonce"
        };

        var json = JsonSerializer.Serialize(data, _outboundOptions);

        await Assert.That(json).Contains("out-key");
        await Assert.That(json).Contains("out-nonce");
        await Assert.That(json).Contains("signature");
    }

    [Test]
    public async Task ForOutbound_NullSignature_OmittedFromJson()
    {
        var data = Data.Ok("no-sig");
        data.Signature = null;

        var json = JsonSerializer.Serialize(data, _outboundOptions);

        await Assert.That(json).DoesNotContain("signature");
    }

    [Test]
    public async Task Roundtrip_SignaturePreserved_ThroughSerializeDeserialize()
    {
        var original = Data.Ok("roundtrip");
        original.Signature = new Signature
        {
            Identity = "rt-key",
            Algorithm = "ed25519",
            Nonce = "rt-nonce",
        };
        original.Signature.GetType().GetProperty("Value")!.SetValue(original.Signature, "base64sig");

        // Serialize with [Out], deserialize with [In]
        var json = JsonSerializer.Serialize(original, _outboundOptions);
        var restored = JsonSerializer.Deserialize<Data>(json, _inboundOptions);

        await Assert.That(restored).IsNotNull();
        await Assert.That(restored!.Signature).IsNotNull();
        await Assert.That(restored.Signature!.Identity).IsEqualTo("rt-key");
        await Assert.That(restored.Signature!.Nonce).IsEqualTo("rt-nonce");
        await Assert.That(restored.Signature!.Algorithm).IsEqualTo("ed25519");
    }

    [Test]
    public async Task NoOpOnTypesWithoutTransportAttributes()
    {
        var obj = new { Name = "plain", Value = 42 };

        var json = JsonSerializer.Serialize(obj, _inboundOptions);

        await Assert.That(json).Contains("plain");
        await Assert.That(json).Contains("42");
    }
}
