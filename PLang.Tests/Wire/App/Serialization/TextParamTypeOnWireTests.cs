namespace PLang.Tests.App.Serialization;

// Does a text param carry its {text} type on the wire? If yes, the read is typed
// (text.Read) and borns the template — no Judge needed. If the type is omitted, the
// read must infer it. This pins down which.
public class TextParamTypeOnWireTests
{
    [Test] public async Task TextParam_CarriesTypeOnWire()
    {
        await using var app = global::PLang.Tests.TestApp.Create(System.IO.Path.Combine(
            System.IO.Path.GetTempPath(), "plang-ptype-" + System.Guid.NewGuid().ToString("N")[..8]));
        var plang = (global::app.channel.serializer.plang.@this)
            app.User.Channel.Serializers.GetByMimeType("application/plang");

        // No Context → no signing, so we see the raw {name, type?, value} shape.
        var data = new global::app.data.@this("Content", "Hi %name%");
        var json = (await plang.Store(data).Value())!.Clr<string>()!;

        // A text param is self-describing — it carries its type, so the read is typed
        // (text.Read borns the template) and no Judge has to infer it.
        await Assert.That(json).Contains("\"type\":{\"name\":\"text\"}");
        await Assert.That(json).Contains("\"value\":\"Hi %name%\"");
    }
}
