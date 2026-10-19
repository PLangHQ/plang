namespace app.type.node.root;

/// <summary>
/// The type system — <c>%!app.type%</c>: the node over the list of every type (a dot picks one by name,
/// <c>%!app.type.image%</c>). Its own member <c>format</c> is the format in play (<c>%!app.type.format%</c>): a kind
/// with a MIME or an extension — the one a goal set, down the goals that called it, else the app's own (plain text
/// unless <c>--format=…</c> named another). Written — <c>set %!app.type.format% to "application/plang"</c> — it is
/// that goal's, and holds for what it calls until it returns. Channels that follow it (the console) write and read
/// in it: plang's own format puts out Data whole, so a program running this plang reads an ask as an ask. Written
/// alone, it is every type with its facts, a structured value its writer lays out.
/// </summary>
public sealed class @this : global::app.type.node.@this<global::app.type.@this, global::app.type.list.@this>
{
    /// <summary>The app's types, their list made here: every other value is born holding its type from it.</summary>
    public @this(global::app.@this app) : this(app, global::app.type.@this.List(app)) { }

    private @this(global::app.@this app, global::app.type.list.@this types) : base(app, types, () => types) { }

    /// <summary>The app's own format, under every goal's: plain text, unless the app was started with another.</summary>
    public global::app.type.kind.@this? Format { get; set; }

    /// <summary>The format in play for <paramref name="context"/>: the nearest goal's that set one, else the app's,
    /// else plain text.</summary>
    public global::app.type.kind.@this FormatOf(global::app.actor.context.@this context)
        => context.call.Format ?? Format ?? list.Mime("text/plain");

    /// <summary>The format <paramref name="named"/> names — by MIME (<c>application/plang</c>), name (<c>json</c>)
    /// or extension (<c>.md</c>); null when no type reads such a format.</summary>
    public global::app.type.kind.@this? Named(string named) => list.Format(named);

    /// <summary>Written alone, the type system: every type plang doesn't keep for itself, each with its facts — its
    /// name (the namespace), the word it goes by when it declares one, its description, example and aliases, and its
    /// kinds' names.</summary>
    public override async System.Threading.Tasks.ValueTask Output(global::app.type.format.IWriter writer,
        global::app.View mode, global::app.actor.context.@this? context)
    {
        if (context == null) { await list.Output(writer, mode, context); return; }
        var types = list.Items().Where(t => !t.Internal).ToList();
        writer.BeginArray(types.Count);
        foreach (var type in types)
        {
            writer.BeginObject();
            writer.Name("name"); writer.String(type.Namespace ?? type.Name);
            if (type.Namespace != null && type.Namespace != type.Name) { writer.Name("word"); writer.String(type.Name); }
            if (await type.Description(context).Text(context) is { Length: > 0 } description) { writer.Name("description"); writer.String(description); }
            if (await type.Example(context).Text(context) is { Length: > 0 } example) { writer.Name("example"); writer.String(example); }
            if (type.Alias.Count > 0)
            {
                writer.Name("alias");
                writer.BeginArray(type.Alias.Count);
                foreach (var alias in type.Alias) writer.String(alias);
                writer.EndArray();
            }
            var kinds = type.kind.list(context).Items().Select(t => t.kind.Name).ToList();
            writer.Name("kind");
            writer.BeginArray(kinds.Count);
            foreach (var kind in kinds) writer.String(kind);
            writer.EndArray();
            writer.EndObject();
        }
        writer.EndArray();
    }

    /// <summary>One step by dot: <c>format</c> is the format in play for the asker; any other key as every node's.</summary>
    public override async System.Threading.Tasks.ValueTask<global::app.data.@this> Get(global::app.data.@this parent, string key)
        => string.Equals(key, "format", System.StringComparison.OrdinalIgnoreCase)
            ? new global::app.data.@this(key, new global::app.type.item.text.@this(Spelled(FormatOf(parent.Context)), list["text"]), parent: parent)
            : await base.Get(parent, key);

    /// <summary><c>format</c> written: the writing goal's format from now until it returns — its call frame's. A
    /// format no type reads is refused, never quiet text.</summary>
    public override async System.Threading.Tasks.ValueTask<global::app.type.item.@this> Set(string key, bool isIndex, object? value,
        global::app.actor.context.@this context)
    {
        if (isIndex || !string.Equals(key, "format", System.StringComparison.OrdinalIgnoreCase))
            return await base.Set(key, isIndex, value, context);
        var text = value is global::app.data.@this d ? (await d.Value())?.ToString() : value?.ToString();
        if (string.IsNullOrWhiteSpace(text) || Named(text) is not { } kind)
            throw new global::app.error.AppException(new global::app.error.@this(
                $"No format '{text}': name one a type reads — a MIME (application/plang), a name (json) or an extension (.md)", "FormatNotFound", 400));
        if (!context.call.SetFormat(kind))
            Format = kind;   // no goal running (the app starting): the app's own
        return this;
    }

    // a format as it is written: its MIME when it has one (application/plang), else its name
    private static string Spelled(global::app.type.kind.@this kind)
        => kind.Mime.Count > 0 ? kind.Mime[0] : kind.Name;
}
