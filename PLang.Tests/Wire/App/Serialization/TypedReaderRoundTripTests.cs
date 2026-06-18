namespace PLang.Tests.App.Serialization;

// Typed (ITypeReader) pull-reader round-trips — the type reads its own value off
// the single decode pass (json.Reader → ITypeReader.Read), no JsonElement DOM.
// Covers the scalars converted in the first increment: bool / guid / duration.
public class TypedReaderRoundTripTests
{
    private static global::app.@this NewApp() => new(System.IO.Path.Combine(
        System.IO.Path.GetTempPath(), "plang-typedread-" + Guid.NewGuid().ToString("N")[..8]));

    private static async Task<(global::app.data.@this readBack, global::app.@this app)> WriteAndRead(object? value)
    {
        var app = NewApp();
        var plang = (global::app.channel.serializer.plang.@this)
            app.User.Channel.Serializers.GetByMimeType("application/plang");

        var data = new global::app.data.@this("v", value) { Context = app.User.Context };
        var wire = (await plang.Serialize(data).Value())!.Clr<string>()!;
        return (plang.Deserialize(wire), app);
    }

    [Test] public async Task Bool_RoundTrips_ThroughTypedReader()
    {
        var (back, app) = await WriteAndRead(true);
        await using (app)
        {
            var item = await back.Value();
            await Assert.That(item!.Clr<bool>()).IsTrue();
        }
    }

    [Test] public async Task Guid_RoundTrips_ThroughTypedReader()
    {
        var g = Guid.NewGuid();
        var (back, app) = await WriteAndRead(g);
        await using (app)
        {
            var item = await back.Value();
            await Assert.That(item!.Clr<Guid>()).IsEqualTo(g);
        }
    }

    [Test] public async Task Duration_RoundTrips_ThroughTypedReader()
    {
        var d = TimeSpan.FromSeconds(30);
        var (back, app) = await WriteAndRead(d);
        await using (app)
        {
            var item = await back.Value();
            await Assert.That(item!.Clr<TimeSpan>()).IsEqualTo(d);
        }
    }
}
