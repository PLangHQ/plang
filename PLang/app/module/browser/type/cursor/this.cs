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

    public override string ToString() => _css;
}
