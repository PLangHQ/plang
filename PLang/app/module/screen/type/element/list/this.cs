namespace app.module.screen.type.element.list;

/// <summary>A screen's elements, picked by selector: <c>%!screen.element["#window.bot"]%</c> — the same element each
/// time it is asked for.</summary>
public sealed class @this(screen.@this screen) : global::app.type.item.@this
{
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, element.@this> _held = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>The element <paramref name="selector"/> picks — the same one each time.</summary>
    internal element.@this Of(string selector) => _held.GetOrAdd(selector.Trim(), s => new element.@this(s, screen));

    /// <summary>The element already asked for under <paramref name="selector"/>, if any (a click on one nobody asked for
    /// has nothing bound).</summary>
    internal element.@this? Held(string selector) => _held.GetValueOrDefault(selector.Trim());

    public override System.Threading.Tasks.ValueTask<global::app.data.@this> Get(global::app.data.@this parent, string key, bool isIndex)
        => Get(parent, key);

    public override System.Threading.Tasks.ValueTask<global::app.data.@this> Get(global::app.data.@this parent, string key)
        => System.Threading.Tasks.ValueTask.FromResult(new global::app.data.@this(key, Of(key), parent: parent));
}
