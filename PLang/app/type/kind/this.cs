namespace app.type.kind;

/// <summary>
/// A kind — the subtype token ("json", "md", "int", "*") that names HOW a value of a type is
/// specialised, AND the behavior for that specialisation. The kind IS the behavior: a value
/// asks its own kind to navigate / enumerate / load / convert / lower it
/// (<c>value.Kind.Navigate(…)</c>) — a direct virtual call, no registry hop.
///
/// <para>This base owns the verb DEFAULTS (a plang-path walk that re-derives the node's kind
/// each hop; the rest throw "not X"). Each real kind subclasses it under the type it
/// specialises (<c>type/item/kind/json</c>, <c>type/number/kind/int</c>) and overrides what it
/// does differently. An UNKNOWN kind ("md", "csv", a host's class name) is just a base instance
/// carrying the name — the defaults are its behavior.</para>
///
/// <para>Selection + lifecycle live on the collection (<c>app.type.Kind[name|clrType]</c>),
/// never a static factory. Equality is by <see cref="Name"/> (case-insensitive); the wire form
/// is the name string.</para>
/// </summary>
public class @this
{
    public string Name { get; }

    /// <summary>Born WITH context — the collection is per-App and mints kinds stamped. Verbs
    /// still take the per-call context as a parameter (a value's context, not the kind's).</summary>
    internal actor.context.@this? Context { get; set; }

    public @this(string name, actor.context.@this? context = null)
    {
        Name = name ?? throw new System.ArgumentNullException(nameof(name));
        Context = context;
    }

    /// <summary>The CLR form values of this kind ride as (json → <c>JsonElement</c>), or null
    /// when the kind claims no single CLR carrier. The collection's <c>[clrType]</c> door reads
    /// this to bridge a raw host to its kind (exact wins, then assignable — <c>IList</c>→list).</summary>
    public virtual System.Type? ClrForm => null;

    /// <summary>
    /// The type a value of this kind narrows to once decoded — the reader's owner type when a
    /// kind-specific reader exists, else the format family, else <c>binary</c>.
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public global::app.type.@this Type
    {
        get
        {
            if (Context == null)
                throw new System.InvalidOperationException(
                    $"kind '{Name}' has no Context — resolving its Type needs a stamped kind.");
            string name = Context.App.Type.Readers.TypeOf(Name)
                          ?? Context.App.Format.TypeOf(Name)
                          ?? "binary";
            return new global::app.type.@this(name, Name) { Context = Context };
        }
    }

    // --- Verbs: the kind owns what you can do with its values. Defaults here; kinds override. ---

    /// <summary>
    /// Walk a value by the plang path, segment by segment, **re-deriving the node's kind after
    /// every hop** — each node is descended by ITS own kind (a goal POCO by <c>*</c>, its Steps
    /// list by the list kind, …). The final node's kind builds the child <c>Data</c>. A kind
    /// whose path language is NOT plang (a future jsonpath) overrides this wholesale.
    /// </summary>
    public virtual async global::System.Threading.Tasks.ValueTask<global::app.data.@this> Navigate(
        object obj, global::app.variable.path.@this path,
        global::app.data.@this parent, global::app.actor.context.@this ctx)
    {
        object? node = obj;
        @this kind = this;                                          // first hop: this carrier's kind
        foreach (var seg in path.Segments)
        {
            if (node is null) return ctx.NotFound(seg.Raw);        // can't descend into null
            // The grammar carries the ask: an Index segment (`[0]`, `[%i%]`) wants a positional
            // answer; a Member segment (`.Count`, `.Name`) wants a named one. A kind that owns
            // both faces (a sequence host: element vs .Count) needs the distinction, so it rides
            // into Descend — the resolved key AND whether it came from an index bracket.
            bool isIndex = seg is global::app.variable.path.Segment.Index;
            string key = seg is global::app.variable.path.Segment.Index i
                ? await i.ResolveKey(ctx.Variable)                 // the ONE bracket-variable resolver
                : ((global::app.variable.path.Segment.Member)seg).Name;
            var (found, next) = kind.Descend(node, key, isIndex, ctx);
            if (!found) return ctx.NotFound(seg.Raw);
            node = next;
            if (node is not null) kind = ctx.App.Type.Kind[node.GetType()];   // re-derive for the next hop
        }
        return kind.Data(parent.Name, node, parent, ctx);
    }

    /// <summary>Descend one level: the value at <paramref name="key"/> on <paramref name="obj"/>,
    /// or <c>(false, null)</c> when absent. <paramref name="isIndex"/> is true when the key came
    /// from an index bracket (`[0]`) rather than a member dot (`.Count`) — a sequence host answers
    /// positional vs named differently. The list kind indexes, the dict kind keys, the <c>*</c>
    /// kind reflects a property — each owns its own descend.</summary>
    public virtual (bool found, object? node) Descend(object obj, string key, bool isIndex, global::app.actor.context.@this ctx)
        => throw new System.NotSupportedException($"kind '{Name}' is not navigable");

    /// <summary>Build the child <c>Data</c> for a landed node. Default: a node that already IS
    /// a Data rides through as itself; otherwise the raw node becomes a child Data (the ctor
    /// lifts it to its plang type / re-derives its kind). json overrides (scalar vs clr(json)).</summary>
    public virtual global::app.data.@this Data(string name, object? node,
        global::app.data.@this? parent, global::app.actor.context.@this ctx)
        => node is global::app.data.@this d ? d
           : new global::app.data.@this(name, node, parent: parent, context: ctx);

    /// <summary>Each child of a container, for <c>foreach</c> — array elements or object members.</summary>
    public virtual System.Collections.Generic.IEnumerable<global::app.data.@this> Enumerate(
        object obj, global::app.actor.context.@this ctx)
        => throw new System.NotSupportedException($"kind '{Name}' is not enumerable");

    /// <summary>Write a child <paramref name="key"/> onto a value of this kind — the kind owns
    /// HOW its content takes a new child. Returns the new value; a kind with no writable content throws.</summary>
    public virtual global::app.type.item.@this Set(
        object host, string key, object? value, global::app.actor.context.@this ctx)
        => throw new System.NotSupportedException($"kind '{Name}' cannot set a child");

    /// <summary>Load a raw payload (string / bytes) into a value OF this kind. The default —
    /// for md and any kind the system doesn't parse — loads it as <c>text</c>. json overrides.</summary>
    public virtual global::System.Threading.Tasks.ValueTask<global::app.data.@this> Load(
        object raw, global::app.actor.context.@this ctx)
        => new(ctx.Ok(new global::app.type.text.@this(raw)));

    /// <summary>Convert a source value INTO a value of this kind — the outbound owns it (dict
    /// from json, audio from text). An error <c>Data</c> when the source can't become this kind.</summary>
    public virtual global::System.Threading.Tasks.ValueTask<global::app.data.@this> Convert(
        global::app.data.@this source, global::app.actor.context.@this ctx)
        => throw new System.NotSupportedException(
            $"cannot convert {source.Type?.Name} into kind '{Name}'");

    /// <summary>Write a host value OF this kind to the wire — json emits raw json, <c>*</c>
    /// reflects a POCO's tagged fields. The carrier delegates its <c>Output</c> here.</summary>
    public virtual global::System.Threading.Tasks.ValueTask Output(
        object obj, global::app.channel.serializer.IWriter writer, global::app.View mode,
        global::app.actor.context.@this? ctx)
        => throw new System.NotSupportedException($"kind '{Name}' cannot write itself");

    /// <summary>Lower a value OF this kind INTO the CLR shape <paramref name="target"/> asks for
    /// — the clr carrier delegates its lower here. json bridges its content to a reader and drives
    /// the <c>*</c> kind's host <c>Read</c>. The default is terminal: a host that isn't already the
    /// target (identity is handled by the carrier) and can't be built genuinely can't lower.</summary>
    public virtual object? Clr(object host, System.Type target, global::app.actor.context.@this ctx)
        => throw new System.InvalidCastException(
            $"a '{Name}' value cannot lower to {target.Name} — the kind cannot build that shape.");

    // OBPV — carry marked, collapse after the restructure compiles: this is a verb+noun name
    // AND a type-switch fork standing in for a value's own self-write. Fix: a reflected value
    // writes itself via `new Data(name, value, ctx).Output(...)` — Data.Output already emits bare
    // vs {name,type,value} envelope by the writer's format (EmitsSchema), not by value type, so
    // the switch below should dissolve. (todos.md 2026-07-09)
    protected async global::System.Threading.Tasks.ValueTask WriteReflected(
        global::app.channel.serializer.IWriter writer, object value, global::app.View mode,
        global::app.actor.context.@this ctx)
    {
        switch (value)
        {
            case global::app.type.item.@this item: await item.Output(writer, mode, ctx); break;
            case global::app.data.@this d: await d.Output(writer, mode, ctx); break;
            case string s: writer.String(s); break;
            // A raw C# scalar the writer renders (number/bool/date/enum/…). Everything else —
            // a collection OR an object — writes through ITS kind (IDictionary → object, IList /
            // any sequence → array, an object → the * kind's declared-face Output). One rule, no
            // categories: the kind decides; an undeclared plang type throws there, loud.
            default:
                if (value.GetType().IsClass) await ctx.App.Type.Kind[value.GetType()].Output(value, writer, mode, ctx);
                else writer.Value(value);
                break;
        }
    }

    public override string ToString() => Name;

    public override bool Equals(object? obj) => obj switch
    {
        @this k => string.Equals(Name, k.Name, System.StringComparison.OrdinalIgnoreCase),
        string s => string.Equals(Name, s, System.StringComparison.OrdinalIgnoreCase),
        _ => false,
    };

    public override int GetHashCode() => System.StringComparer.OrdinalIgnoreCase.GetHashCode(Name);
}
