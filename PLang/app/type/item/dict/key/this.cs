namespace app.type.item.dict.key;

/// <summary>
/// A dict's key — <c>%dict!key%</c>. Read alone it is the dict's first key, its default one; <c>%dict!key.list%</c>
/// is every key, in the order they were set. It holds its dict, never a copy of the keys: a key set later is in it.
/// </summary>
[global::app.Attributes.PlangType("key")]
public sealed class @this : global::app.type.item.@this
{
    private readonly global::app.type.item.dict.@this _dict;

    /// <summary>The key of <paramref name="dict"/>, born holding the app's <paramref name="type"/> for it.</summary>
    internal @this(global::app.type.item.dict.@this dict, global::app.type.@this type) : base(type) => _dict = dict;

    /// <summary>A dict's own node, never LLM vocabulary — kept out of the catalog.</summary>
    public static bool Internal => true;

    /// <summary>Every key, in order — a list of text, each made by the asker's types.</summary>
    [global::app.LlmBuilder]
    public global::app.type.item.list.@this<global::app.type.item.text.@this> list(global::app.actor.context.@this context)
    {
        var text = context.App.type.list["text"];
        var keys = new global::app.type.item.list.@this<global::app.type.item.text.@this>(
            context.App.type.list[typeof(global::app.type.item.list.@this<global::app.type.item.text.@this>)]);
        foreach (var name in _dict.KeyNames)
            keys.Add(new global::app.data.@this(name, text.Make(name, context), context: context));
        return keys;
    }

    /// <summary>Read alone, the first key: the default one.</summary>
    internal override bool IsFinal => false;

    /// <summary>The first key as text, made by the asker's types; the null value for a dict with no key.</summary>
    public override System.Threading.Tasks.ValueTask<global::app.type.item.@this> Value(global::app.data.@this data)
        => new(First is not { } name ? data.Empty
            : data.Context is { } context ? context.App.type.list["text"].Make(name, context)
            : (global::app.type.item.text.@this)name);

    /// <summary><c>.list</c> is every key; any other step walks the first key.</summary>
    public override async System.Threading.Tasks.ValueTask<global::app.data.@this> Get(global::app.data.@this parent, string key)
        => string.Equals(key, nameof(list), System.StringComparison.OrdinalIgnoreCase) && parent.Context is { } context
            ? new global::app.data.@this(key, list(context), parent: parent)
            : await base.Get(parent, key);

    /// <summary>A key writes the first key's name; a dict with none writes null.</summary>
    protected internal override System.Threading.Tasks.ValueTask Write(global::app.type.format.IWriter writer)
    {
        if (First is { } name) writer.String(name);
        else writer.Null();
        return default;
    }

    public override string ToString() => First ?? "";

    // the dict's first key, null when it has none
    private string? First => _dict.KeyNames.FirstOrDefault();
}
