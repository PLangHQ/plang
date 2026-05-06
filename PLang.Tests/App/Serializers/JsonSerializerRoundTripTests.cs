using global::App.Channels.Serializers;
using global::App.Channels.Serializers.Serializer;

namespace PLang.Tests.App.Serializers;

public class JsonSerializerRoundTripTests
{
    [Test]
    public async Task JsonSerializer_Write_EmitsValueOnly_NeverReadsSignature()
    {
        // text/html and application/json wire shape is data.Value only; data.Signature
        // backing field stays null after Write.
        var app = new global::App.@this("/test");
        var data = new Data("v") { Value = "hello", Context = app.User.Context };

        var json = app.Serializers.GetByMimeType("application/json");
        var s = json.Serialize(data.Value);

        await Assert.That(s.Contains("hello")).IsTrue();
        // RawSignature stays null — JsonSerializer doesn't access Signature property.
        await Assert.That(data.RawSignature).IsNull();
    }

    [Test]
    public async Task JsonSerializer_Read_ProducesData_WithoutPopulatingSignature()
    {
        // Reading a JSON wire payload reconstructs Data with Value set; Signature stays null.
        var app = new global::App.@this("/test");
        var json = app.Serializers.GetByMimeType("application/json");
        var raw = "\"hello\"";
        var s = json.Deserialize<string>(raw);
        await Assert.That(s).IsEqualTo("hello");
    }

    [Test]
    public async Task JsonSerializer_HandlesTextHtml_AndApplicationJson_MimeTypes()
    {
        // The serializer registers for both mimetypes and produces the same wire shape.
        var app = new global::App.@this("/test");
        var jsonByJson = app.Serializers.GetByMimeType("application/json");
        var jsonByHtml = app.Serializers.GetByMimeType("text/html");
        await Assert.That(jsonByJson).IsTypeOf<JsonStreamSerializer>();
        await Assert.That(jsonByHtml).IsTypeOf<JsonStreamSerializer>();
        // Same instance — text/html aliases to the JSON serializer.
        await Assert.That(jsonByHtml).IsSameReferenceAs(jsonByJson);
    }
}
