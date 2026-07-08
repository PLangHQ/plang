namespace app.type.kind;

/// <summary>
/// A kind value — the subtype token ("json", "md", "int") that names HOW a value of a
/// type is specialised. It is the single door to a kind's BEHAVIOR: a value asks its own
/// kind to navigate / enumerate / load / convert it (<c>value.Kind.Navigate(…)</c>), and
/// the token delegates to the registered <see cref="behavior.@this"/> for its name. So
/// there is never a "kinds collection" on the type — you already hold the kind.
///
/// <para>Also the reader-side mapping: <see cref="Type"/> is the type a value of this kind
/// narrows to (json→item, csv→table, mp3→audio), via the reader / format registry.</para>
/// </summary>
public sealed class @this
{
    public string Name { get; }

    /// <summary>Context is deferred: the <see cref="op_Implicit(string)"/> mints a
    /// context-less token (<c>Kind = "json"</c>); it is stamped when the token is used in
    /// a context, and the behavior verbs take the context as a parameter regardless.</summary>
    internal actor.context.@this? Context { get; set; }

    public @this(string name, actor.context.@this? context = null)
    {
        Name = name ?? throw new System.ArgumentNullException(nameof(name));
        Context = context;
    }

    /// <summary>A bare non-null kind name IS a kind — eases the literal <c>Kind = "json"</c>.
    /// A NULLABLE string is never implicitly a kind (a class conversion can't null-lift, and a
    /// null-returning operator is a landmine): assign such sites explicitly
    /// (<c>k is null ? null : (kind)k</c>). There is deliberately NO <c>kind → string</c>
    /// implicit — read a kind's name with <c>.Name</c>/<c>.ToString()</c>, null-safe.</summary>
    public static implicit operator @this(string name) => new(name);

    /// <summary>The kind for a name, or null for a null/absent name — the explicit door for a
    /// NULLABLE string (the implicit above is non-null only, and an object-initializer
    /// <c>{ Kind = someString? }</c> would otherwise invoke it on null and throw). Use this
    /// wherever the source name may be null.</summary>
    public static @this? Of(string? name) => name is null ? null : new(name);

    /// <summary>
    /// The type a value of this kind narrows to once decoded — the reader's owner type
    /// when a kind-specific reader exists, else the format family, else <c>binary</c>.
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public global::app.type.@this Type
    {
        get
        {
            if (Context == null)
                throw new System.InvalidOperationException(
                    $"kind '{Name}' has no Context — resolving its Type needs a stamped token.");
            string name = Context.App.Type.Readers.TypeOf(Name)
                          ?? Context.App.Format.TypeOf(Name)
                          ?? "binary";
            return new global::app.type.@this(name, Name) { Context = Context };
        }
    }

    // --- Behavior: the kind owns what you can do with its values, delegating by name. ---

    /// <summary>Navigate a value of this kind by <paramref name="path"/>.</summary>
    public global::System.Threading.Tasks.ValueTask<global::app.data.@this> Navigate(
        object obj, global::app.variable.path.@this path,
        global::app.data.@this parent, global::app.actor.context.@this ctx)
        => ctx.App.Type.Kinds[this].Navigate(obj, path, parent, ctx);

    /// <summary>Enumerate the children of a container value of this kind (for foreach).</summary>
    public System.Collections.Generic.IEnumerable<global::app.data.@this> Enumerate(
        object obj, global::app.actor.context.@this ctx)
        => ctx.App.Type.Kinds[this].Enumerate(obj, ctx);

    /// <summary>Convert <paramref name="source"/> INTO a value of this kind — the outbound
    /// owns it (dict from json, audio from text). An error <c>Data</c> when it can't.</summary>
    public global::System.Threading.Tasks.ValueTask<global::app.data.@this> Convert(
        global::app.data.@this source, global::app.actor.context.@this ctx)
        => ctx.App.Type.Kinds[this].Convert(source, ctx);

    public override string ToString() => Name;

    public override bool Equals(object? obj) => obj switch
    {
        @this k => string.Equals(Name, k.Name, System.StringComparison.OrdinalIgnoreCase),
        string s => string.Equals(Name, s, System.StringComparison.OrdinalIgnoreCase),
        _ => false,
    };

    public override int GetHashCode() => System.StringComparer.OrdinalIgnoreCase.GetHashCode(Name);
}
