using System.Text.Json;
using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;

namespace PLang.Tests.App.TypeKindStrict.TypeValueModelTests;

// `type` is the structured entity on the wire — ONE field carrying
// `{name, kind?, strict?}`, no flat sibling `kind` key.
public class WireKindShapeTests
{
    private static JsonSerializerOptions Options
        => new global::app.channel.serializer.plang.@this(global::PLang.Tests.TestApp.SharedContext)
            .GetType()
            .GetField("_outbound", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .GetValue(new global::app.channel.serializer.plang.@this(global::PLang.Tests.TestApp.SharedContext))
            as JsonSerializerOptions
            ?? throw new System.InvalidOperationException("could not access plang outbound options");

    private static string ToJson(global::app.data.@this data) => JsonSerializer.Serialize(data, Options);
    private static global::app.data.@this FromJson(string json) => JsonSerializer.Deserialize<global::app.data.@this>(json, Options)!;

    [Test] public async Task Wire_Write_EmitsTypeAsStructuredEntity()
    {
        var d = new global::app.data.@this("x", "hi", new global::app.type.@this("text", "md"), context: global::PLang.Tests.TestApp.SharedContext);
        var json = ToJson(d);
        // ONE `type` field carrying the dict. No flat sibling `kind` key.
        await Assert.That(json.Contains("\"type\":{\"name\":\"text\",\"kind\":\"md\"}")).IsTrue();
        await Assert.That(System.Text.RegularExpressions.Regex.IsMatch(json, @"""kind""\s*:\s*""md""\s*,")).IsFalse();
    }

    [Test] public async Task Wire_Write_OmitsKindWhenNull()
    {
        var d = new global::app.data.@this("x", "hi", new global::app.type.@this("text"), context: global::PLang.Tests.TestApp.SharedContext);
        var json = ToJson(d);
        await Assert.That(json.Contains("\"type\":{\"name\":\"text\"}")).IsTrue();
        await Assert.That(json.Contains("\"kind\"")).IsFalse();
    }

    [Test] public async Task Wire_Write_NoTypeColonKindCompositeString()
    {
        var d = new global::app.data.@this("x", "hi", new global::app.type.@this("text", "md"), context: global::PLang.Tests.TestApp.SharedContext);
        var json = ToJson(d);
        await Assert.That(json.Contains("text:md")).IsFalse();
        await Assert.That(json.Contains("\"text/md\"")).IsFalse();
    }

    [Test] public async Task Wire_RoundTrip_PreservesNameKindStrict()
    {
        var d = new global::app.data.@this("x", "data", new global::app.type.@this("image", "gif"), context: global::PLang.Tests.TestApp.SharedContext);
        var json = ToJson(d);
        var roundTripped = FromJson(json);
        await Assert.That(roundTripped.Type.Name).IsEqualTo("image");
        await Assert.That(roundTripped.Type.Kind).IsEqualTo("gif");
        await Assert.That(roundTripped.Type.Strict).IsFalse();
    }

    [Test] public async Task Wire_Read_StructuredTypeShape_Deserializes()
    {
        var json = "{\"name\":\"x\",\"type\":{\"name\":\"text\",\"kind\":\"md\"},\"value\":\"hi\"}";
        var d = FromJson(json);
        await Assert.That(d.Type.Name).IsEqualTo("text");
        await Assert.That(d.Type.Kind).IsEqualTo("md");
        await Assert.That(d.Kind).IsEqualTo("md");
    }

    [Test] public async Task Wire_Write_OmitsTypeForNullSentinel()
    {
        var d = new global::app.data.@this("x", null, global::app.type.@this.Null);
        var json = ToJson(d);
        await Assert.That(json.Contains("\"type\"")).IsFalse();
    }
}
