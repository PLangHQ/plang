using text = global::app.type.item.text.@this;

namespace PLang.Tests.App.Types;

/// <summary>
/// Stage 1 (item-doors): a plain text's OWN kind answers that it's characters — no Name
/// floor. Today <c>source.cs:115</c> reads <c>_type.kind.IsText || _type.Name is "text"</c>,
/// because text's own format carries no <c>Text = true</c> (<c>type/item/text/this.cs:30</c>:
/// <c>[Format("", "text/plain", ".txt")]</c>) and the kind built inline from the name has
/// <c>IsText = false</c>. The floor patches it. Red today because <c>text.Type.kind.IsText</c>
/// is false without asking the type's name.
/// </summary>
public class TextKindIsTextByItselfTests : System.IAsyncDisposable
{
    private readonly global::app.@this app = new global::app.@this("/app").Testing();
    public async System.Threading.Tasks.ValueTask DisposeAsync() => await app.DisposeAsync();

    [Test] public async Task PlainText_Kind_IsText_WithoutNameFloor()
    {
        var t = new text("hello");
        await Assert.That(t.Type.kind).IsNotNull();
        await Assert.That(t.Type.kind.IsText).IsTrue();
    }
}
