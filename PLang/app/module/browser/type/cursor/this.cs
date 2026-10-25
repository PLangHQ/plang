namespace app.module.browser.type.cursor;

/// <summary>
/// PLang <c>cursor</c> value — the pointer a page wants where the mouse is: its CSS name (<c>pointer</c>, <c>text</c>,
/// <c>default</c>, <c>grab</c> …). A headless browser sends it beside its frames when it changes, and the window showing
/// them turns it into the system's pointer. It writes itself as <c>{"cursor": "pointer"}</c>.
/// </summary>
[global::app.Attributes.PlangType("cursor")]
public sealed class @this : global::app.type.item.@this, global::app.type.item.ICreate<@this>
{
    private readonly string _css;

    public @this(string css) => _css = css;

    /// <summary>Its CSS name.</summary>
    [Out] public global::app.type.item.text.@this Css => _css;

    protected internal override System.Threading.Tasks.ValueTask Write(global::app.type.format.IWriter writer)
    {
        writer.BeginObject();
        writer.Name("cursor"); writer.String(_css);
        writer.EndObject();
        return default;
    }

    /// <summary>The line it goes up as, beside a browser's frames: written as text, one compact line whatever json's
    /// layout — the window finds the cursor in a text line only.</summary>
    internal async System.Threading.Tasks.Task<string?> Line(global::app.actor.context.@this context)
    {
        using var stream = new System.IO.MemoryStream();
        var encoded = await context.App.type.list["text"].kind.Encode(stream, context.Ok(this), context);
        return encoded.Success ? System.Text.Encoding.UTF8.GetString(stream.ToArray()) : null;
    }

    public override string ToString() => _css;
}
