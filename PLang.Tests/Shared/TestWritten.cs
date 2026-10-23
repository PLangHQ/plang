namespace PLang.Tests;

/// <summary>A value as text writes it out — through its Output, by the text format, as a template or a channel
/// shows it; never a ToString.</summary>
public static class TestWritten
{
    public static async System.Threading.Tasks.Task<string> Written(this global::app.actor.context.@this context,
        global::app.type.item.@this value)
    {
        using var ms = new System.IO.MemoryStream();
        var written = await context.App.type.list["text"].kind.Encode(ms, context.Ok(value), context, global::app.View.Out);
        if (!written.Success) throw new System.InvalidOperationException($"the value didn't write as text: {written.Error!.Message}");
        return System.Text.Encoding.UTF8.GetString(ms.ToArray());
    }
}
