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
        var node = new List<data.@this>
        {
            new("$type", e.GetType().Name),
            new("id", e.Id),
            new("message", e.Message),
            new("key", e.Key),
            new("statusCode", e.StatusCode),
            new("createdUtc", e.CreatedUtc),
            new("category", e.Category.ToString()),
        };
        if (e.FixSuggestion != null) node.Add(new("fixSuggestion", e.FixSuggestion));
        if (e.HelpfulLinks != null) node.Add(new("helpfulLinks", e.HelpfulLinks));

        if (e is AskError ask)
        {
            node.Add(new("table", ask.Table));
            node.Add(new("dataKey", ask.DataKey));
        }

        if (e.ErrorChain is { Count: > 0 })
        {
            var chain = new List<object?>();
            foreach (var c in e.ErrorChain) chain.Add(new TypedValueNode(c, "error"));
            node.Add(new("errorChain", chain));
        }
        return node;
    }
}
