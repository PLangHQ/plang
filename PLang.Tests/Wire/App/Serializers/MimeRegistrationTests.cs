using app.channel.serializer;
using app.channel.serializer;

namespace PLang.Tests.App.Serializers;

public class MimeRegistrationTests
{
    [Test]
    public async Task Channels_LookupSerializerByMimeType_RoutesAccordingly()
    {
        var app = global::PLang.Tests.TestApp.Create("/test");
        var json = app.User.Channel.Serializers.GetByMimeType("application/json");
        var plang = app.User.Channel.Serializers.GetByMimeType("application/plang");
        var text = app.User.Channel.Serializers.GetByMimeType("text/plain");

        await Assert.That(json).IsTypeOf<global::app.channel.serializer.Json>();
        await Assert.That(plang).IsTypeOf<global::app.channel.serializer.plang.@this>();
        await Assert.That(text).IsTypeOf<global::app.channel.serializer.Text>();
    }

    [Test]
    public async Task Channels_UnregisteredMimeType_RaisesError()
    {
        // No silent fallback — names + integrity model says hard error.
        var app = global::PLang.Tests.TestApp.Create("/test");
        await Assert.ThrowsAsync<UnregisteredMimeType>((Func<Task>)(async () =>
        {
            app.User.Channel.Serializers.GetByMimeType("application/x-totally-made-up");
            await Task.CompletedTask;
        }));
    }

    [Test]
    public async Task ApplicationPlangData_Mime_NoLongerRegistered_MergedIntoApplicationPlang()
    {
        // Stage 2: the separate "application/plang+data" wire shape collapsed
        // into "application/plang". GetByType returns null; GetByMimeType throws.
        var app = global::PLang.Tests.TestApp.Create("/test");
        var s = app.User.Channel.Serializers.GetByType("application/plang+data");
        await Assert.That(s).IsNull();
    }
}
