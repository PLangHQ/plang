namespace PLang.Tests.App.Serialization;

// A format is found on the type list by its MIME or its extension — case-insensitive, with or without the
// leading dot. (FormatKindTests pins the MIME parameter stripping, text/plain, unknown MIME and plang's own.)
public class FormatLookupTests
{
    private static global::app.actor.context.@this Ctx => global::PLang.Tests.TestApp.SharedContext;

    [Test]
    public async Task Mime_ApplicationJson_IsItemsJson()
    {
        var type = Ctx.App.type.list.Mime("application/json", Ctx);
        await Assert.That(type.Name).IsEqualTo("item");
        await Assert.That(type.kind.Name).IsEqualTo("json");
    }

    [Test]
    public async Task Mime_TextJson_IsTheSameJsonFormat()
    {
        await Assert.That(Ctx.App.type.list.Mime("text/json", Ctx).kind.Name).IsEqualTo("json");
    }

    [Test]
    public async Task Mime_IsCaseInsensitive()
    {
        await Assert.That(Ctx.App.type.list.Mime("APPLICATION/JSON", Ctx).kind.Name).IsEqualTo("json");
        await Assert.That(Ctx.App.type.list.Mime("Application/Json", Ctx).kind.Name).IsEqualTo("json");
    }

    [Test]
    public async Task Extension_Json_IsItemsJson()
    {
        var type = Ctx.App.type.list.Extension(".json", Ctx);
        await Assert.That(type.Name).IsEqualTo("item");
        await Assert.That(type.kind.Name).IsEqualTo("json");
    }

    [Test]
    public async Task Extension_WithoutDot_IsTheSameFormat()
    {
        await Assert.That(Ctx.App.type.list.Extension("json", Ctx).kind.Name).IsEqualTo("json");
    }

    [Test]
    public async Task Extension_Txt_IsText()
    {
        var type = Ctx.App.type.list.Extension(".txt", Ctx);
        await Assert.That(type.Name).IsEqualTo("text");
        await Assert.That(type.kind.IsEmpty).IsTrue();
    }

    [Test]
    public async Task Extension_IsCaseInsensitive()
    {
        await Assert.That(Ctx.App.type.list.Extension(".JSON", Ctx).kind.Name).IsEqualTo("json");
    }

    [Test]
    public async Task Extension_Unknown_IsBytesOfThatKind()
    {
        var type = Ctx.App.type.list.Extension(".xyz", Ctx);
        await Assert.That(type.Name).IsEqualTo("binary");
        await Assert.That(type.kind.Name).IsEqualTo("xyz");
    }
}
