namespace app.type.node.root;

/// <summary>
/// The type system — <c>%!app.type%</c>: the node over the list of every type (a dot picks one by name,
/// <c>%!app.type.image%</c>). Written alone, it is every type with its facts, a structured value its writer lays out.
/// </summary>
public sealed class @this : global::app.type.node.@this<global::app.type.@this, global::app.type.list.@this>
{
    /// <summary>The app's types, their list made here: every other value is born holding its type from it.</summary>
    public @this(global::app.@this app) : this(app, global::app.type.@this.List(app)) { }

    // The app's types, over the list it is born with: they are its own types too.
    private @this(global::app.@this app, global::app.type.list.@this types) : base(app, types[typeof(global::app.type.node.@this)], types) { }

    /// <summary>One step by dot: <c>kind</c> is item's kinds (<c>%!app.type.kind.json%</c>, as <c>type/item/kind/json</c>
    /// reads, a kind's settings there: <c>%!app.type.kind.json.setting%</c>); any other key is the node's own members,
    /// then one type by name.</summary>
    public override async System.Threading.Tasks.ValueTask<global::app.data.@this> Get(global::app.data.@this parent, string key)
        => string.Equals(key, "kind", System.StringComparison.OrdinalIgnoreCase)
            ? new global::app.data.@this(key, list["item"].kind, parent: parent)
            : await base.Get(parent, key);

    /// <summary>Written alone, the type system: every type plang doesn't keep for itself, each with its facts — its
    /// name (the namespace), the word it goes by when it declares one, its description, example and aliases, and its
    /// kinds' names.</summary>
    protected internal override async System.Threading.Tasks.ValueTask Write(global::app.type.format.IWriter writer)
    {
        var types = list.Items().Where(t => !t.Internal).ToList();
        writer.BeginArray(types.Count);
        foreach (var type in types)
        {
            writer.BeginObject();
            writer.Name("name"); writer.String(type.Namespace ?? type.Name);
            if (type.Namespace != null && type.Namespace != type.Name) { writer.Name("word"); writer.String(type.Name); }
            if (await type.Description(writer.Context).Text(writer.Context) is { Length: > 0 } description) { writer.Name("description"); writer.String(description); }
            if (await type.Example(writer.Context).Text(writer.Context) is { Length: > 0 } example) { writer.Name("example"); writer.String(example); }
            if (type.Alias.Count > 0)
            {
                writer.Name("alias");
                writer.BeginArray(type.Alias.Count);
                foreach (var alias in type.Alias) writer.String(alias);
                writer.EndArray();
            }
            var kinds = type.kind.list(writer.Context).Items().Select(t => t.kind.Name).ToList();
            writer.Name("kind");
            writer.BeginArray(kinds.Count);
            foreach (var kind in kinds) writer.String(kind);
            writer.EndArray();
            writer.EndObject();
        }
        writer.EndArray();
    }
}
