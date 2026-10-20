namespace PLang.Tests;

/// <summary>
/// Test-only: a template string rendered the way the runtime renders one — a text born with the
/// marker, held by a Data and written out into a text writer: what crosses (Out) uses it, each %var% through its own
/// Output. The variable store has no render of its own.
/// </summary>
public static class TemplateRender
{
    public static async Task<string> Rendered(this global::app.actor.context.@this context, string template)
    {
        using var ms = new System.IO.MemoryStream();
        var writer = new global::app.type.item.text.Writer(ms, System.Text.Encoding.UTF8, global::app.type.item.culture.@this.Create("en-US", context.App.type.list[typeof(global::app.type.item.culture.@this)])!, global::app.View.Out, context);
        var text = new global::app.type.item.text.@this(template,
            context.App.type.list[new global::app.type.@this("text", template: new global::app.type.item.template.kind.plang.@this()), context]);
        await new global::app.data.@this("", text, context: context).Output(writer, writer.View, context);
        return System.Text.Encoding.UTF8.GetString(ms.ToArray());
    }
}
