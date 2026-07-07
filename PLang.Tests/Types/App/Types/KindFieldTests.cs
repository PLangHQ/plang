using System.Text.Json;

namespace PLang.Tests.App.Types;

// Wire shape: `type` is the structured entity `{name, kind?, strict?}` —
// one field carrying full identity, no flat sibling `kind` key.
public class KindFieldTests : System.IAsyncDisposable
{
    private readonly global::app.@this _app = global::PLang.Tests.TestApp.Create("/tmp/kindfield-" + System.Guid.NewGuid().ToString("N")[..6]);
    public async System.Threading.Tasks.ValueTask DisposeAsync() => await _app.DisposeAsync();

    // A Data writes itself via Data.Output through the serializer's async path (the Out view),
    // NOT JsonSerializer.Serialize — the Wire converter is read-only and throws on STJ Write.
    private static string ToJson(global::app.data.@this data)
        => new global::app.channel.serializer.plang.@this(global::PLang.Tests.TestApp.SharedContext)
            .Serialize(data).Peek()!.ToString()!;

    private static global::app.data.@this FromJson(string json)
        => new global::app.channel.serializer.plang.@this(global::PLang.Tests.TestApp.SharedContext).Deserialize(json);

    [Test]
    public async Task PrParameter_OmitsKindWhenAbsent()
    {
        var data = new global::app.data.@this("x", "hello", new global::app.type.@this("text"), context: _app.User.Context);
        var json = ToJson(data);
        await Assert.That(json.Contains("\"type\":{\"name\":\"text\"}")).IsTrue();
        await Assert.That(json.Contains("\"kind\"")).IsFalse();
    }

    [Test]
    public async Task PrParameter_TypeCarriesKindInsideTheStructuredEntity()
    {
        var data = new global::app.data.@this("photo", "/srv/a.jpg", new global::app.type.@this("path", "file"), context: _app.User.Context);
        var json = ToJson(data);
        await Assert.That(json.Contains("\"type\":{\"name\":\"path\",\"kind\":\"file\"}")).IsTrue();
    }

    [Test]
    public async Task PrParameter_NeverColonStringForTypeKind()
    {
        var data = new global::app.data.@this("p", "http://x", new global::app.type.@this("path", "http"), context: _app.User.Context);
        var json = ToJson(data);
        await Assert.That(json.Contains("\"path:http\"")).IsFalse();
        await Assert.That(json.Contains("\"type\":\"path/http\"")).IsFalse();
    }

    [Test]
    public async Task PrParameter_KindNull_OmittedFromWire()
    {
        var data = new global::app.data.@this("x", 1, new global::app.type.@this("number"), context: _app.User.Context);
        var json = ToJson(data);
        await Assert.That(json.Contains("\"kind\":null")).IsFalse();
    }

    [Test]
    public async Task PrParameter_RoundTrip_PreservesKindAcrossWriteAndRead()
    {
        var original = new global::app.data.@this("photo", "/srv/a.jpg", new global::app.type.@this("path", "file"), context: _app.User.Context);
        var json = ToJson(original);
        var read = FromJson(json);
        await Assert.That(read.Type?.Name).IsEqualTo("path");
        await Assert.That(read.Type?.Kind).IsEqualTo("file");
        await Assert.That(read.Kind).IsEqualTo("file");
    }
}
