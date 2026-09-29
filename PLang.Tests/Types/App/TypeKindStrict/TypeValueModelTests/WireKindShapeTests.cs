using System.Text.Json;
using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;

namespace PLang.Tests.App.TypeKindStrict.TypeValueModelTests;

// `type` is the structured entity on the wire — ONE field carrying
// `{name, kind?, strict?}`, no flat sibling `kind` key.
public class WireKindShapeTests : System.IAsyncDisposable
{
    private readonly global::app.@this app = new global::app.@this("/app").Testing();
    public async System.Threading.Tasks.ValueTask DisposeAsync() => await app.DisposeAsync();

    // A Data writes itself via Data.Output through the serializer's async path (the Out view),
    // NOT JsonSerializer.Serialize — the Wire converter is read-only and throws on STJ Write.
    private string ToJson(global::app.data.@this data)
        => app.actor.list.User.Context.Format("application/plang")
            .Serialize(data, app.actor.list.User.Context).Peek()!.ToString()!;
    private global::app.data.@this FromJson(string json)
        => app.actor.list.User.Context.Format("application/plang").Stored(json, app.actor.list.User.Context);

    [Test] public async Task Wire_Write_EmitsTypeAsStructuredEntity()
    {
        var d = new global::app.data.@this("x", "hi", new global::app.type.@this("text", "md"), context: app.actor.list.User.Context);
        var json = ToJson(d);
        // ONE `type` field carrying the dict. No flat sibling `kind` key.
        await Assert.That(json.Contains("\"type\":{\"name\":\"text\",\"kind\":\"md\"}")).IsTrue();
        await Assert.That(System.Text.RegularExpressions.Regex.IsMatch(json, @"""kind""\s*:\s*""md""\s*,")).IsFalse();
    }

    [Test] public async Task Wire_Write_OmitsKindWhenNull()
    {
        var d = new global::app.data.@this("x", "hi", new global::app.type.@this("text"), context: app.actor.list.User.Context);
        var json = ToJson(d);
        await Assert.That(json.Contains("\"type\":{\"name\":\"text\"}")).IsTrue();
        await Assert.That(json.Contains("\"kind\"")).IsFalse();
    }

    [Test] public async Task Wire_Write_NoTypeColonKindCompositeString()
    {
        var d = new global::app.data.@this("x", "hi", new global::app.type.@this("text", "md"), context: app.actor.list.User.Context);
        var json = ToJson(d);
        await Assert.That(json.Contains("text:md")).IsFalse();
        await Assert.That(json.Contains("\"text/md\"")).IsFalse();
    }

    [Test] public async Task Wire_RoundTrip_PreservesNameKindStrict()
    {
        var d = new global::app.data.@this("x", "data", new global::app.type.@this("image", "gif"), context: app.actor.list.User.Context);
        var json = ToJson(d);
        var roundTripped = FromJson(json);
        await Assert.That(roundTripped.Type.Name).IsEqualTo("image");
        await Assert.That(roundTripped.Type.kind.Name).IsEqualTo("gif");
        await Assert.That(roundTripped.Type.Strict).IsFalse();
    }

    [Test] public async Task Wire_Read_StructuredTypeShape_Deserializes()
    {
        var json = "{\"name\":\"x\",\"type\":{\"name\":\"text\",\"kind\":\"md\"},\"value\":\"hi\"}";
        var d = FromJson(json);
        await Assert.That(d.Type.Name).IsEqualTo("text");
        await Assert.That(d.Type.kind.Name).IsEqualTo("md");
        await Assert.That(d.Kind).IsEqualTo("md");
    }

    [Test] public async Task Wire_Write_OmitsTypeForNullSentinel()
    {
        var d = new global::app.data.@this("x", null, global::app.type.@this.Null);
        var json = ToJson(d);
        // the data row itself — inside the signing layer the wire wraps it in, which has a type of its own
        using var doc = System.Text.Json.JsonDocument.Parse(json);
        var root = doc.RootElement;
        var row = root.TryGetProperty("@schema", out var schema) && schema.GetString() == "signature"
            ? root.GetProperty("value") : root;
        await Assert.That(row.TryGetProperty("type", out _)).IsFalse();
    }
}
