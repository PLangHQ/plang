using app.channels.serializers;
using app.channels.serializers.serializer;

namespace PLang.Tests.App.Serializers;

public class MimeRegistrationTests
{
    [Test]
    public async Task Channels_LookupSerializerByMimeType_RoutesAccordingly()
    {
        var app = new global::app.@this("/test");
        var json = app.User.Channels.Serializers.GetByMimeType("application/json");
        var plang = app.User.Channels.Serializers.GetByMimeType("application/plang");
        var text = app.User.Channels.Serializers.GetByMimeType("text/plain");

        await Assert.That(json).IsTypeOf<global::app.channels.serializers.serializer.Json>();
        await Assert.That(plang).IsTypeOf<global::app.channels.serializers.serializer.plang.@this>();
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
    public async Task ApplicationPlangData_Mime_NoLongerRegistered_MergedIntoApplicationPlang()
    {
        // Stage 2: the separate "application/plang+data" wire shape collapsed
        // into "application/plang". GetByType returns null; GetByMimeType throws.
        var app = new global::app.@this("/test");
        var s = app.User.Channels.Serializers.GetByType("application/plang+data");
        await Assert.That(s).IsNull();
    }
}
