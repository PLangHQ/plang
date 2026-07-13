namespace PLang.Tests.App.Serialization;

using Ctx = global::app.actor.context.@this;

/// <summary>
/// text.Writer.Value must dispatch a top-level ITEM through its own Write door — a leaf
/// renders BARE (hello, 42), a container renders as JSON — matching item.Write(textWriter)
/// and json.Writer's own item arm. Without this a top-level item fell to the json delegate
/// and came out quoted.
/// </summary>
public class TextWriterItemArmTests
{
    private static readonly Ctx C = global::PLang.Tests.TestApp.SharedContext;

    private static string Render(global::app.type.item.@this item)
    {
        using var ms = new System.IO.MemoryStream();
        var w = new global::app.channel.serializer.text.Writer(ms, System.Text.Encoding.UTF8);
        w.Value(item);
        return System.Text.Encoding.UTF8.GetString(ms.ToArray());
    }

    [Test]
    public async Task TopLevelTextItem_RendersBare_NotQuoted()
        => await Assert.That(Render(new global::app.type.item.text.@this("hello"))).IsEqualTo("hello");

    [Test]
    public async Task TopLevelNumberItem_RendersBareLiteral()
        => await Assert.That(Render((global::app.type.item.number.@this)42)).IsEqualTo("42");

    [Test]
    public async Task TopLevelDictItem_RendersJson()
    {
        var d = new global::app.type.item.dict.@this(C);
        d.Set("a", 1);
        await Assert.That(Render(d)).IsEqualTo("{\"a\":1}");
    }
}
