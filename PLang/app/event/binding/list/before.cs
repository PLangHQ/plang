namespace app.@event.binding.list;

/// <summary>
/// What starts before an event. A binding that fails stops what it is before — its error is the answer — and one
/// that answers Handled cancels it: its answer is the result, and the bindings after it don't start. A plain
/// success says nothing; the result passes on as is.
/// </summary>
public sealed class before : @this
{
    /// <summary>An own event's list: it takes bindings.</summary>
    internal before() : base(open: true) { }

    private before(bool open) : base(open) { }

    /// <summary>The before of the shared empty events: closed, it never holds a binding.</summary>
    internal static readonly before None = new(open: false);

    private protected override async System.Threading.Tasks.ValueTask<global::app.data.@this> Run(binding.@this[] bindings,
        global::app.type.item.@this item, global::app.data.@this result, global::app.actor.context.@this context)
    {
        foreach (var binding in bindings)
        {
            if (!binding.For(item, context)) continue;
            var answer = await binding.Start(item, result, context);
            if (!answer.Success || answer.Handled) return answer;
        }
        return result;
    }
}
