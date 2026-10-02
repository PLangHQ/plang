namespace PLang.Tests;

/// <summary>
/// Test-only: a template string rendered the way the runtime renders one — a text born with the
/// marker, writing itself through its own door into a text writer (each %var% through its own
/// Output). The variable store has no render of its own.
/// </summary>
public static class TemplateRender
{
    public static async Task<string> Rendered(this global::app.actor.context.@this context, string template)
    {
        using var ms = new System.IO.MemoryStream();
        var writer = new global::app.type.item.text.Writer(ms, System.Text.Encoding.UTF8, global::app.type.item.culture.@this.Create("en-US")!);
        await new global::app.type.item.text.@this(template, new global::app.type.item.template.kind.plang.@this()).Output(writer, global::app.View.Out, context);
        return System.Text.Encoding.UTF8.GetString(ms.ToArray());
    }
}
