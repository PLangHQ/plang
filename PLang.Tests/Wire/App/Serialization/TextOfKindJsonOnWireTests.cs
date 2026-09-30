namespace PLang.Tests.App.Serialization;

// A text of kind json (a rendered .json template) inside plang's own envelope: the envelope names its type, so the
// value is the string it is — and it reads back as that text of kind json, never as a json object.
public class TextOfKindJsonOnWireTests
{
    [Test] public async Task TextOfKindJson_ThroughPlang_ReadsBackAsTextOfKindJson()
    {
        await using var app = new global::app.@this(System.IO.Path.Combine(
            System.IO.Path.GetTempPath(), "plang-textjson-" + System.Guid.NewGuid().ToString("N")[..8])).Testing();
        var ctx = app.actor.list.User.Context;
        var plang = ctx.Format("application/plang");
        var data = new global::app.data.@this("body",
            new global::app.type.item.text.@this("{\"deep\": \"m-1\"}") { Kind = "json" }, context: ctx);

        var written = (await plang.Store(data, ctx).Value())!.Clr<string>()!;
        var back = plang.Stored(written, ctx);
        var value = await back.Value();

        // the envelope names the type; the value is a string
        await Assert.That(written).Contains("\"type\":{\"name\":\"text\",\"kind\":\"json\"},\"value\":\"");
        await back.IsSuccess();
        await Assert.That(value).IsTypeOf<global::app.type.item.text.@this>();
        await Assert.That(value!.ToString()).IsEqualTo("{\"deep\": \"m-1\"}");
        await Assert.That(back.Type.kind.Name).IsEqualTo("json");
    }
}
