using System.Text.Json;

namespace PLang.Tests.App.Types;

// plang-types — Stage 1
// .pr parameter shape grows an optional `kind` sibling to `type`.
// NEVER a "type:kind" string — splitting a string is runtime work.

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
    public async Task PrParameter_HasOptionalKindField_OmittedWhenAbsent()
    {
        // No kind set ⇒ no "kind" key on the wire.
        var data = new global::app.data.@this("x", "hello")
        {
            Type = new global::app.type.@this("string")
        };
        var json = ToJson(data);
        await Assert.That(json.Contains("\"kind\"")).IsFalse();
    }

    [Test]
    public async Task PrParameter_KindWritten_WhenTypeProducesOne()
    {
        // Builder sets Kind explicitly after calling Types.Kinds.Of(...);
        // the wire emits "kind":"<value>" alongside "type".
        var data = new global::app.data.@this("photo", "/srv/a.jpg")
        {
            Type = new global::app.type.@this("path"),
            Kind = "file"
        };
        var json = ToJson(data);
        await Assert.That(json.Contains("\"kind\":\"file\"")).IsTrue();
        await Assert.That(json.Contains("\"type\":\"path\"")).IsTrue();
    }

    [Test]
    public async Task PrParameter_KindAndTypeAreSeparateFields_NeverColonString()
    {
        // The fields are separate. Combined "type:kind" form must never appear.
        var data = new global::app.data.@this("p", "http://x")
        {
            Type = new global::app.type.@this("path"),
            Kind = "http"
        };
        var json = ToJson(data);
        await Assert.That(json.Contains("\"path:http\"")).IsFalse();
        await Assert.That(json.Contains("\"path\"")).IsTrue();
        await Assert.That(json.Contains("\"http\"")).IsTrue();
    }

    [Test]
    public async Task PrParameter_KindNull_NotSerializedAsLiteralNull()
    {
        var data = new global::app.data.@this("x", 1)
        {
            Type = new global::app.type.@this("int"),
            Kind = null,
        };
        var json = ToJson(data);
        await Assert.That(json.Contains("\"kind\":null")).IsFalse();
        await Assert.That(json.Contains("\"kind\"")).IsFalse();
    }

    [Test]
    public async Task PrParameter_RoundTrip_PreservesKindAcrossWriteAndRead()
    {
        var original = new global::app.data.@this("photo", "/srv/a.jpg")
        {
            Type = new global::app.type.@this("path"),
            Kind = "file"
        };
        var json = ToJson(original);
        var read = FromJson(json);
        await Assert.That(read.Type?.Value).IsEqualTo("path");
        await Assert.That(read.Kind).IsEqualTo("file");
    }
}
