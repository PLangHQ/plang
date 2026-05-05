using global::App.Channels.Serializers;
using global::App.Channels.Serializers.Serializer;

namespace PLang.Tests.App.Serializers;

public class MimeRegistrationTests
{
    [Test]
    public async Task Channels_LookupSerializerByMimeType_RoutesAccordingly()
    {
        var app = new global::App.@this("/test");
        var json = app.Channels.Serializers.GetByMimeType("application/json");
        var pdata = app.Channels.Serializers.GetByMimeType("application/plang+data");
        var text = app.Channels.Serializers.GetByMimeType("text/plain");

        await Assert.That(json).IsTypeOf<JsonStreamSerializer>();
        await Assert.That(pdata).IsTypeOf<PlangDataSerializer>();
        await Assert.That(text).IsTypeOf<TextStreamSerializer>();
    }

    [Test]
    public async Task Channels_UnregisteredMimeType_RaisesError()
    {
        // No silent fallback — names + integrity model says hard error.
        var app = new global::App.@this("/test");
        await Assert.ThrowsAsync<UnregisteredMimeType>(async () =>
        {
            app.Channels.Serializers.GetByMimeType("application/x-totally-made-up");
            await Task.CompletedTask;
        });
    }

    [Test]
    public async Task ApplicationPlangData_Mime_RegisteredAtAppBoot()
    {
        var app = new global::App.@this("/test");
        var s = app.Channels.Serializers.GetByMimeType("application/plang+data");
        await Assert.That(s).IsTypeOf<PlangDataSerializer>();
    }
}
