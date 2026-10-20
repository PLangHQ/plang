using text = global::app.type.item.text.@this;

namespace PLang.Tests.App.Types;

/// <summary>
/// Stage 1 (item-doors, decisions 867 + 880): a plain text's OWN kind answers that it's
/// characters — no Name floor. The value is born through the app's type door
/// (<c>app.type.list["text"].Create("hello", ctx)</c>), not via <c>new</c>, since no
/// value is made without its app (880).
///
/// <para>Red today: text's own format (<c>type/item/text/this.cs:30</c>:
/// <c>[Format("", "text/plain", ".txt")]</c>) carries no <c>Text = true</c>, so the
/// value's <c>Type.kind.IsText</c> is false. Today <c>source.cs:115</c> patches it with
/// <c>_type.Name is "text"</c>. The stage makes the kind carry IsText from the registered
/// facts (type born whole), without the Name fallback.</para>
/// </summary>
public class TextKindIsTextByItselfTests : System.IAsyncDisposable
{
    private readonly global::app.@this app = global::PLang.Tests.Session.App("/app").Testing();
    public async System.Threading.Tasks.ValueTask DisposeAsync() => await app.DisposeAsync();

    [Test] public async Task PlainText_Kind_IsText_WithoutNameFloor()
    {
        var ctx = app.actor.list.System.Context;
        var data = await app.type.list["text"].Create("hello", ctx);
        var t = (text)await data.Value();

        await Assert.That(t.Type.kind).IsNotNull();
        await Assert.That(t.Type.kind.IsText).IsTrue();
    }
}
