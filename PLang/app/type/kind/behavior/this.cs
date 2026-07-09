namespace app.type.kind.behavior;

/// <summary>
/// The behavior of a kind — what you can DO with a value of one kind: navigate it,
/// enumerate it, load one from raw, convert into it. One class per kind (json, <c>*</c>,
/// dict, …); a format is added by adding a class. Reached through the kind token
/// (<see cref="app.type.kind.@this"/>), which delegates here by name — so a value asks
/// its own kind (<c>value.Kind.Navigate(…)</c>), never a collection.
///
/// <para>The base gives a default plang-path walk (<see cref="Navigate"/> loops the
/// already-tokenized <c>path.Segments</c>, resolving a bracket variable via the one
/// resolver <c>Segment.Index.ResolveKey</c>) and errors for a capability a kind does not
/// provide. A kind whose path language is NOT plang (a future jsonpath / css kind)
/// overrides <see cref="Navigate"/> wholesale.</para>
/// </summary>
public abstract class @this
{
    /// <summary>The kind this behaves as ("json", "*", "dict"). Keys the registry, and is
    /// the token a <c>clr</c> takes directly as its own kind.</summary>
    public abstract global::app.type.kind.@this Kind { get; }

    /// <summary>The CLR form values of this kind ride as (json → <c>JsonElement</c>),
    /// or null when the kind has no single CLR carrier. The CLR-bridge fact that lets a
    /// <c>clr</c> resolve an unstamped host to its kind (fast dict, default <c>*</c>).</summary>
    public virtual System.Type? ClrForm => null;

    /// <summary>
    /// Walk a value OF this kind by the plang path, segment by segment. A container node
    /// stays raw for the next hop; the last hop builds the child <c>Data</c> (container →
    /// clr, scalar → its plang scalar). A missing segment is NotFound.
    /// </summary>
    public virtual async global::System.Threading.Tasks.ValueTask<global::app.data.@this> Navigate(
        object obj, global::app.variable.path.@this path,
        global::app.data.@this parent, global::app.actor.context.@this ctx)
    {
        object? node = obj;
        foreach (var seg in path.Segments)
        {
            string key = seg is global::app.variable.path.Segment.Index i
                ? await i.ResolveKey(ctx.Variable)                                   // the ONE bracket-variable resolver
                : ((global::app.variable.path.Segment.Member)seg).Name;
            var (found, next) = Step(node!, key, ctx);
            if (!found) return ctx.NotFound(seg.Raw);
            node = next;
        }
        return Data(parent.Name, node, parent, ctx);
    }

    /// <summary>Descend one level: the value at <paramref name="key"/> on
    /// <paramref name="obj"/>, or <c>(false, null)</c> when absent.</summary>
    protected virtual (bool found, object? node) Step(object obj, string key, global::app.actor.context.@this ctx)
        => throw new System.NotSupportedException($"kind '{Kind}' is not navigable");

    /// <summary>Build the child <c>Data</c> for a landed node — the factory that gives us
    /// Data back. Container → a clr (its kind derives again); scalar → its plang scalar.
    /// Never a clr wrapping a scalar.</summary>
    protected virtual global::app.data.@this Data(string name, object? node,
        global::app.data.@this? parent, global::app.actor.context.@this ctx)
        => throw new System.NotSupportedException($"kind '{Kind}' is not navigable");

    /// <summary>Each child of a container, for <c>foreach</c> — array elements or object
    /// members, each a <c>Data</c>.</summary>
    public virtual System.Collections.Generic.IEnumerable<global::app.data.@this> Enumerate(
        object obj, global::app.actor.context.@this ctx)
        => throw new System.NotSupportedException($"kind '{Kind}' is not enumerable");

    /// <summary>Write a child <paramref name="key"/> onto a value of this kind — the kind
    /// owns HOW its content takes a new child. json materializes its immutable object into a
    /// mutable <c>dict</c> (members staying lazy clr(json)) and sets the key, so a later
    /// <c>%x.child%</c> still navigates — never reflecting the carrier's C# surface. Returns
    /// the new value; a kind with no writable content throws.</summary>
    public virtual global::app.type.item.@this Set(
        object host, string key, object? value, global::app.actor.context.@this ctx)
        => throw new System.NotSupportedException($"kind '{Kind}' cannot set a child");

    /// <summary>Load a raw payload (string / bytes) into a value OF this kind. The default —
    /// for md and any kind the system doesn't parse — loads it as <c>text</c> (the raw stands
    /// as its own value). The json kind overrides to parse into a clr(json).</summary>
    public virtual global::System.Threading.Tasks.ValueTask<global::app.data.@this> Load(
        object raw, global::app.actor.context.@this ctx)
        => new(ctx.Ok(new global::app.type.text.@this(raw)));

    /// <summary>Convert a source value INTO a value of this kind — the outbound owns it
    /// (dict builds itself from json; audio from text). Returns the built value or an
    /// error <c>Data</c> when the source can't become this kind.</summary>
    public virtual global::System.Threading.Tasks.ValueTask<global::app.data.@this> Convert(
        global::app.data.@this source, global::app.actor.context.@this ctx)
        => throw new System.NotSupportedException(
            $"cannot convert {source.Type?.Name} into kind '{Kind}'");

    /// <summary>Write a host value OF this kind to the wire — the json kind emits its raw
    /// json (never reflecting the <c>JsonElement</c>'s BCL props); the <c>*</c> kind reflects
    /// a POCO's <c>[Out]</c> fields. The carrier delegates its <c>Output</c> here.</summary>
    public virtual global::System.Threading.Tasks.ValueTask Output(
        object obj, global::app.channel.serializer.IWriter writer, global::app.View mode,
        global::app.actor.context.@this? ctx)
        => throw new System.NotSupportedException($"kind '{Kind}' cannot write itself");

    /// <summary>Lower a value OF this kind INTO the CLR shape <paramref name="target"/> asks
    /// for — the clr carrier delegates its lower here, so the kind owns it. The json kind
    /// overrides to bridge its content to a reader and drive the <c>*</c> kind's host
    /// <c>Read</c>. The default is terminal: a host that isn't already the target (identity is
    /// handled by the carrier) and can't be built genuinely can't lower.</summary>
    public virtual object? Clr(object host, System.Type target, global::app.actor.context.@this ctx)
        => throw new System.InvalidCastException(
            $"a '{Kind}' value cannot lower to {target.Name} — the kind cannot build that shape.");
}
