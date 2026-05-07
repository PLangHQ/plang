using global::App.Channels.Serializers;
using global::App.Channels.Serializers.Serializer;

namespace PLang.Tests.App.Serializers;

public class MimeRegistrationTests
{
    [Test]
    public async Task Channels_LookupSerializerByMimeType_RoutesAccordingly()
    {
        var app = new global::App.@this("/test");
        var json = app.Serializers.GetByMimeType("application/json");
        var pdata = app.Serializers.GetByMimeType("application/plang+data");
        var text = app.Serializers.GetByMimeType("text/plain");

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
            app.Serializers.GetByMimeType("application/x-totally-made-up");
            await Task.CompletedTask;
        });
    }

    [Test]
    public async Task ApplicationPlangData_Mime_RegisteredAtAppBoot()
    {
        var app = new global::App.@this("/test");
        var s = app.Serializers.GetByMimeType("application/plang+data");
        await Assert.That(s).IsTypeOf<PlangDataSerializer>();
    }
}
