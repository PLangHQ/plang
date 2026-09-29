namespace app.type.kind.empty;

/// <summary>
/// A type's kind when it has none — a bare <c>text</c> has text's empty kind. It knows the type it
/// belongs to and holds that type's kinds (number's precisions, choice's sets, path's schemes,
/// item's json/list/dict/<c>*</c>, setting's classes); it answers for them by name, by C# form, and as
/// <c>list</c> (each as the full type it makes). Its name is empty and it is never written.
/// </summary>
public sealed class @this : global::app.type.kind.@this
{
    private readonly string _owner;
    private readonly System.Collections.Generic.List<global::app.type.kind.@this> _kinds = new();
    private readonly object _gate = new();
    // The type's own format — its class's [Format("", …)]: plain text is {text}, opaque bytes {binary}.
    private global::app.type.kind.@this? _format;

    public @this(string owner) : base("") => _owner = owner;

    protected internal override string Owner => _owner;

    public override System.Collections.Generic.IReadOnlyList<string> Mime => _format?.Mime ?? [];
    public override System.Collections.Generic.IReadOnlyList<string> Extension => _format?.Extension ?? [];
    public override bool Compressible => _format?.Compressible ?? false;
    public override bool IsText => _format?.IsText ?? false;

    /// <summary>The type's own format writes it (<c>.pr</c> for goal); a type with none writes nothing.</summary>
    public override System.Threading.Tasks.Task<global::app.data.@this> Encode(System.IO.Stream stream,
        global::app.data.@this data, global::app.actor.context.@this context, global::app.View? view = null,
        System.Text.Encoding? encoding = null, System.Threading.CancellationToken ct = default)
        => _format != null ? _format.Encode(stream, data, context, view, encoding, ct)
            : base.Encode(stream, data, context, view, encoding, ct);

    // The kinds this type holds, in the order they came.
    private global::app.type.kind.@this[] Held
    {
        get { lock (_gate) return _kinds.ToArray(); }
    }

    /// <summary>Everything this type holds: its own format, if it has one, then its kinds — what a type
    /// object that replaces this one's type takes over.</summary>
    internal System.Collections.Generic.IEnumerable<global::app.type.kind.@this> Kinds
        => _format is { } format ? Held.Prepend(format) : Held;

    /// <summary>The type itself when it answers to <paramref name="name"/> (its own MIME or extension), else one
    /// of its kinds that does — by name, alias, MIME or extension; null when none does.</summary>
    public override global::app.type.kind.@this? this[string name]
    {
        get
        {
            if (Names(name)) return this;
            lock (_gate)
                foreach (var kind in _kinds)
                    if (kind.Names(name)) return kind;
            return null;
        }
    }

    /// <summary>The kind one of this type's kinds coins for <paramref name="name"/> (a list's element kinds);
    /// null when none coins one.</summary>
    public override global::app.type.kind.@this? Coin(string name)
    {
        foreach (var kind in Held)
            if (kind.Coin(name) is { } coined) return coined;
        return null;
    }

    /// <summary>One of this type's kinds by the C# form its values ride as — each kind says whether it
    /// carries the class; exact wins, then the most derived; null when none carries it.</summary>
    public override global::app.type.kind.@this? this[System.Type clr]
    {
        get
        {
            global::app.type.kind.@this? best = null;
            foreach (var k in Held)
            {
                if (!k.Carries(clr)) continue;
                if (k.ClrForm == clr) return k;
                if (best is null || best.ClrForm!.IsAssignableFrom(k.ClrForm!)) best = k;
            }
            return best;
        }
    }

    /// <summary>This type's kinds, each as the full type it makes.</summary>
    public override global::app.type.kind.list.@this list(global::app.actor.context.@this context)
        => new(Held.Select(k => context.App.type.list[new global::app.type.@this(_owner, k.Name), context]));

    /// <summary>Adds a kind of this type; a kind of the same name replaces the one before it. The type's own
    /// format (the empty name) is held as the type's, not as a kind.</summary>
    internal void Add(global::app.type.kind.@this kind)
    {
        lock (_gate)
        {
            if (kind.IsEmpty) { _format = kind; return; }
            _kinds.RemoveAll(k => string.Equals(k.Name, kind.Name, System.StringComparison.OrdinalIgnoreCase));
            _kinds.Add(kind);
        }
    }
}
