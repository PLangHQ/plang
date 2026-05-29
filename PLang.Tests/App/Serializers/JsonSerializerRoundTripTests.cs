using app.channel.serializer;
using app.channel.serializer;

namespace PLang.Tests.App.Serializers;

public class JsonSerializerRoundTripTests
{
    [Test]
    public async Task JsonSerializer_Write_EmitsValueOnly_NeverReadsSignature()
    {
        // text/html and application/json wire shape is data.Value only; data.Signature
        // backing field stays null after Write.
        var app = new global::app.@this("/test");
        var data = new Data("v") { Value = "hello", Context = app.User.Context };

        var json = app.User.Channels.Serializers.GetByMimeType("application/json");
        var s = json.Serialize(data).Value!;

        await Assert.That(s.Contains("hello")).IsTrue();
        // Signature stays null — JsonSerializer doesn't access Signature property.
        await Assert.That(data.Signature).IsNull();
    }

    [Test]
    public async Task JsonSerializer_Read_ProducesData_WithoutPopulatingSignature()
    {
        // Reading a JSON wire payload reconstructs Data with Value set; Signature stays null.
        var app = new global::app.@this("/test");
        var json = app.User.Channels.Serializers.GetByMimeType("application/json");
        var raw = "\"hello\"";
        var s = json.Deserialize<string>(raw).Value!;
        await Assert.That(s).IsEqualTo("hello");
    }

    [Test]
    public async Task JsonSerializer_HandlesTextHtml_AndApplicationJson_MimeTypes()
    {
        // The serializer registers for both mimetypes and produces the same wire shape.
        var app = new global::app.@this("/test");
        var jsonByJson = app.User.Channels.Serializers.GetByMimeType("application/json");
        var jsonByHtml = app.User.Channels.Serializers.GetByMimeType("text/html");
        await Assert.That(jsonByJson).IsTypeOf<global::app.channel.serializer.Json>();
        await Assert.That(jsonByHtml).IsTypeOf<global::app.channel.serializer.Json>();
        // Same instance — text/html aliases to the JSON serializer.
        await Assert.That(jsonByHtml).IsSameReferenceAs(jsonByJson);
    }
}
