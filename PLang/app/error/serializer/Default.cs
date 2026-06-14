using app.channel.serializer;
using app.data;

namespace app.error.serializer;

/// <summary>
/// <see cref="IError"/>'s leaf-serializer — an error owns its wire shape. The
/// flattened form: a <c>$type</c> discriminator + the reportable content
/// (almost no subclass carries its own state) + the recursive ErrorChain. The
/// live back-references that can't round-trip (Exception, Step, Goal,
/// CallFrames) are dropped — the snapshot's CallStack section carries the chain.
/// Symmetric with the read side (<see cref="global::app.error.ErrorWire"/>).
/// </summary>
public static class Default
{
    public static void Write(IError e, IWriter writer)
        => writer.Value(Render(e));

    private static object? Render(IError e)
    {
        // The error renders as an object — a native dict keyed by field name, not
        // a List<Data> property bag. The writer emits dict as `{}`, which is what
        // the read side (ErrorWire) expects.
        var node = new global::app.type.dict.@this();
        node.Set(new("$type", e.GetType().Name));
        node.Set(new("id", e.Id));
        node.Set(new("message", e.Message));
        node.Set(new("key", e.Key));
        node.Set(new("statusCode", e.StatusCode));
        node.Set(new("createdUtc", e.CreatedUtc));
        node.Set(new("category", e.Category.ToString()));
        if (e.FixSuggestion != null) node.Set(new("fixSuggestion", e.FixSuggestion));
        if (e.HelpfulLinks != null) node.Set(new("helpfulLinks", e.HelpfulLinks));

        if (e is AskError ask)
        {
            node.Set(new("table", ask.Table));
            node.Set(new("dataKey", ask.DataKey));
        }

        if (e.ErrorChain is { Count: > 0 })
        {
            var chain = new List<object?>();
            foreach (var c in e.ErrorChain) chain.Add(c); // each error is an item — it renders itself
            node.Set(new("errorChain", chain));
        }
        return node;
    }
}
