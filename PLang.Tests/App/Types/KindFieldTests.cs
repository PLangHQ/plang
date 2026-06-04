using System.Text.Json;

namespace PLang.Tests.App.Types;

// Wire shape: `type` is the structured entity `{name, kind?, strict?}` —
// one field carrying full identity, no flat sibling `kind` key.
public class KindFieldTests
{
    private static JsonSerializerOptions Options
        => global::app.channel.serializer.plang.@this.ContextLessFallback
            .GetType()
            .GetField("_outbound", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .GetValue(global::app.channel.serializer.plang.@this.ContextLessFallback)
            as JsonSerializerOptions
            ?? throw new System.InvalidOperationException("could not access plang outbound options");

    private static string ToJson(global::app.data.@this data)
        => JsonSerializer.Serialize(data, Options);

    private static global::app.data.@this FromJson(string json)
        => JsonSerializer.Deserialize<global::app.data.@this>(json, Options)!;

    [Test]
    public async Task PrParameter_OmitsKindWhenAbsent()
    {
        var data = new global::app.data.@this("x", "hello")
        {
            Type = new global::app.type.@this("text")
        };
        var json = ToJson(data);
        await Assert.That(json.Contains("\"type\":{\"name\":\"text\"}")).IsTrue();
        await Assert.That(json.Contains("\"kind\"")).IsFalse();
    }

    [Test]
    public async Task PrParameter_TypeCarriesKindInsideTheStructuredEntity()
    {
        var data = new global::app.data.@this("photo", "/srv/a.jpg")
        {
            Type = new global::app.type.@this("path", "file")
        };
        var json = ToJson(data);
        await Assert.That(json.Contains("\"type\":{\"name\":\"path\",\"kind\":\"file\"}")).IsTrue();
    }

    [Test]
    public async Task PrParameter_NeverColonStringForTypeKind()
    {
        var data = new global::app.data.@this("p", "http://x")
        {
            Type = new global::app.type.@this("path", "http")
        };
        var json = ToJson(data);
        await Assert.That(json.Contains("\"path:http\"")).IsFalse();
        await Assert.That(json.Contains("\"type\":\"path/http\"")).IsFalse();
    }

    [Test]
    public async Task PrParameter_KindNull_OmittedFromWire()
    {
        var data = new global::app.data.@this("x", 1)
        {
            Type = new global::app.type.@this("number"),
        };
        var json = ToJson(data);
        await Assert.That(json.Contains("\"kind\":null")).IsFalse();
    }

    [Test]
    public async Task PrParameter_RoundTrip_PreservesKindAcrossWriteAndRead()
    {
        var original = new global::app.data.@this("photo", "/srv/a.jpg")
        {
            Type = new global::app.type.@this("path", "file")
        };
        var json = ToJson(original);
        var read = FromJson(json);
        await Assert.That(read.Type?.Name).IsEqualTo("path");
        await Assert.That(read.Type?.Kind).IsEqualTo("file");
        await Assert.That(read.Kind).IsEqualTo("file");
    }
}
