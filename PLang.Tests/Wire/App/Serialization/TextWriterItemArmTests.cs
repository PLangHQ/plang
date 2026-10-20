namespace PLang.Tests.App.Serialization;

using Ctx = global::app.actor.context.@this;

/// <summary>
/// text.Writer.Value must dispatch a top-level ITEM through its own Write door — a leaf
/// renders BARE (hello, 42), a container renders as JSON — matching item.Write(textWriter)
/// and json.Writer's own item arm. Without this a top-level item fell to the json delegate
/// and came out quoted.
/// </summary>
public class TextWriterItemArmTests : System.IAsyncDisposable
{
    private readonly global::app.@this app = global::PLang.Tests.Session.App("/app").Testing();
    public async System.Threading.Tasks.ValueTask DisposeAsync() => await app.DisposeAsync();

    private Ctx C => app.actor.list.User.Context;

    private async Task<string> Render(global::app.type.item.@this item)
    {
        using var ms = new System.IO.MemoryStream();
        var w = new global::app.type.item.text.Writer(ms, System.Text.Encoding.UTF8, global::app.type.item.culture.@this.Create("en-US", C.App.type.list[typeof(global::app.type.item.culture.@this)])!, C.App.type.list);
        await w.Kind.pair.Output(item, w, global::app.View.Out, C);
        return System.Text.Encoding.UTF8.GetString(ms.ToArray());
    }

    [Test]
    public async Task TopLevelTextItem_RendersBare_NotQuoted()
        => await Assert.That(await Render(C.Text("hello"))).IsEqualTo("hello");

    [Test]
    public async Task TopLevelNumberItem_RendersBareLiteral()
        => await Assert.That(await Render((global::app.type.item.number.@this)42)).IsEqualTo("42");

    [Test]
    public async Task TopLevelDictItem_RendersJson()
    {
        var d = C.Dict();
        d.Set("a", 1);
        await Assert.That(await Render(d)).IsEqualTo("{\"a\":1}");
    }
}
