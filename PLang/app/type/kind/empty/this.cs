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

    public @this(string owner) : base("") => _owner = owner;

    protected internal override string Owner => _owner;

    // The kinds this type holds, in the order they came.
    private global::app.type.kind.@this[] Held
    {
        get { lock (_gate) return _kinds.ToArray(); }
    }

    /// <summary>One of this type's kinds, by its name or an alias; null when it holds none by that name.</summary>
    public override global::app.type.kind.@this? this[string name]
        => Held.FirstOrDefault(k => string.Equals(k.Name, name, System.StringComparison.OrdinalIgnoreCase)
                                    || k.Alias.Contains(name, System.StringComparer.OrdinalIgnoreCase));

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

    /// <summary>This type's kinds, each as the full type it makes, with its family's formats.</summary>
    public override global::app.type.kind.list.@this list(global::app.actor.context.@this context)
    {
        var formats = context.App.Format.KindsByFamily().TryGetValue(_owner, out var family) ? family : [];
        return new(Held.Select(k => k.Name).Concat(formats).Distinct(System.StringComparer.OrdinalIgnoreCase)
            .Select(name => context.App.type.list[new global::app.type.@this(_owner, name), context]));
    }

    /// <summary>Adds a kind of this type; a kind of the same name replaces the one before it.</summary>
    internal void Add(global::app.type.kind.@this kind)
    {
        lock (_gate)
        {
            _kinds.RemoveAll(k => string.Equals(k.Name, kind.Name, System.StringComparison.OrdinalIgnoreCase));
            _kinds.Add(kind);
        }
    }
}
