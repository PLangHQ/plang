using global::app.channels.serializers;
using global::app.channels.serializers.serializer;

namespace PLang.Tests.App.Serializers;

public class MimeRegistrationTests
{
    [Test]
    public async Task Channels_LookupSerializerByMimeType_RoutesAccordingly()
    {
        var app = new global::app.@this("/test");
        var json = app.User.Channels.Serializers.GetByMimeType("application/json");
        var pdata = app.User.Channels.Serializers.GetByMimeType("application/plang+data");
        var text = app.User.Channels.Serializers.GetByMimeType("text/plain");

        await Assert.That(json).IsTypeOf<global::app.channels.serializers.serializer.Json>();
        await Assert.That(pdata).IsTypeOf<global::app.channels.serializers.serializer.plang.Data>();
        await Assert.That(text).IsTypeOf<global::app.channels.serializers.serializer.Text>();
    }

    [Test]
    public async Task Channels_UnregisteredMimeType_RaisesError()
    {
        // No silent fallback — names + integrity model says hard error.
        var app = new global::app.@this("/test");
        await Assert.ThrowsAsync<UnregisteredMimeType>(async () =>
        {
            app.User.Channels.Serializers.GetByMimeType("application/x-totally-made-up");
            await Task.CompletedTask;
        });
    }

    [Test]
    public async Task ApplicationPlangData_Mime_RegisteredAtAppBoot()
    {
        var app = new global::app.@this("/test");
        var s = app.User.Channels.Serializers.GetByMimeType("application/plang+data");
        await Assert.That(s).IsTypeOf<global::app.channels.serializers.serializer.plang.Data>();
    }
}
