namespace app.type.kind.empty;

/// <summary>
/// A type's kind when it has none — a bare <c>text</c> has text's empty kind. It knows the type it
/// belongs to and holds that type's kinds (number's precisions, choice's sets, path's schemes,
/// item's json/list/dict/<c>*</c>, setting's classes); it answers for them by name, by C# form, and as
/// <c>list</c> (each as the full type it makes). Its name is empty and it is never written.
/// </summary>
public sealed class @this : global::app.type.kind.@this
{
    private readonly global::app.type.@this _owner;
    private readonly System.Collections.Generic.List<global::app.type.kind.@this> _kinds = new();
    // The held kinds by every key each answers to (Names): its name, its aliases, its MIMEs — and apart, by its
    // extensions without the dot — the first kind that answers, in the order they came. Made whole, at its size, the
    // first time a key is looked up after kinds came in (Add drops it), so a key finds its kind in one look, never by
    // asking each kind in turn; none while nothing looks a key up (a type object made for a value, the common case,
    // and a type whose kinds are only walked, never pay for one).
    private System.Collections.Generic.Dictionary<string, global::app.type.kind.@this>? _keys;
    private System.Collections.Generic.Dictionary<string, global::app.type.kind.@this>? _extensions;
    private readonly object _gate = new();
    // The type's own format — its class's [Format("", …)]: plain text is {text}, opaque bytes {binary}.
    private global::app.type.kind.@this? _format;
    // Whether the kinds the code gives the type are held: 0 not yet, 1 being held (a kind read while they are — the
    // same thread — sees those held so far), 2 held.
    private int _ready;

    /// <summary>The empty kind of <paramref name="owner"/>, the type it is born by.</summary>
    public @this(global::app.type.@this owner, global::app.type.kind.@this? format = null) : base("")
    {
        _owner = owner;
        _format = format;
    }

    // The kinds the code gives the type, held the first time any of its kinds is asked: a type nothing asks for its
    // kinds never makes them. The list the type is an entry of holds them; a type built from its class in no list
    // (a C# operator's birth) takes the format its class declares as its own; any other holds what it was given.
    private void Ready()
    {
        if (System.Threading.Volatile.Read(ref _ready) == 2) return;
        lock (_gate)
        {
            if (_ready != 0) return;
            _ready = 1;
            if (_owner.Types is { } list) list.Enlist(_owner);
            else _format ??= _owner.Declared("");
            System.Threading.Volatile.Write(ref _ready, 2);
        }
    }

    // The type's own format, once its kinds are held.
    private global::app.type.kind.@this? Format
    {
        get
        {
            Ready();
            return _format;
        }
    }

    /// <summary>The empty kind is its own kind — ends the <c>kind : type</c> ctor's kind-minting chain
    /// without a new allocation. (Serializers are kept off <c>type.kind</c>, so this self-reference
    /// never loops a walk.)</summary>
    protected override global::app.type.kind.@this BornKind() => this;

    protected internal override global::app.type.@this Owner => _owner;

    public override System.Collections.Generic.IReadOnlyList<string> Mime => Format?.Mime ?? [];
    public override System.Collections.Generic.IReadOnlyList<string> Extension => Format?.Extension ?? [];
    public override bool Compressible => Format?.Compressible ?? false;
    public override bool IsText => Format?.IsText ?? false;

    /// <summary>The type's own format writes it (<c>.pr</c> for goal); a type with none writes nothing.</summary>
    public override System.Threading.Tasks.Task<global::app.data.@this> Encode(System.IO.Stream stream,
        global::app.data.@this data, global::app.actor.context.@this context, global::app.View? view = null,
        System.Text.Encoding? encoding = null, System.Threading.CancellationToken ct = default)
        => Format is { } format ? format.Encode(stream, data, context, view, encoding, ct)
            : base.Encode(stream, data, context, view, encoding, ct);

    /// <summary>The type's own format reads it (<c>.pr</c> for goal); a type with none has its content born lazily.</summary>
    public override System.Threading.Tasks.Task<global::app.data.@this> Decode(byte[] raw,
        global::app.actor.context.@this context, string name = "", global::app.View view = global::app.View.Out,
        System.Threading.CancellationToken ct = default, global::app.type.item.template.kind.@this? template = null, global::app.type.item.path.@this? origin = null)
        => Format is { } format ? format.Decode(raw, context, name, view, ct, template, origin)
            : base.Decode(raw, context, name, view, ct, template, origin);

    /// <summary>What a value of the type can be in <paramref name="step"/>: its own kind's answer when it has one (a
    /// conversation offers only what can continue one), else the step's variables.</summary>
    public override System.Threading.Tasks.ValueTask<System.Collections.Generic.IReadOnlyList<global::app.type.item.@this>> Offers(global::app.goal.step.@this step)
        => Format?.Offers(step) ?? base.Offers(step);

    /// <summary>Closed as the type's own kind is (a bool's); a type with none offers an open set.</summary>
    public override bool IsClosed => Format?.IsClosed ?? false;

    // The kinds this type holds, in the order they came.
    private global::app.type.kind.@this[] Held
    {
        get
        {
            Ready();
            lock (_gate) return _kinds.ToArray();
        }
    }

    /// <summary>Everything this type holds: its own format, if it has one, then its kinds — what a type
    /// object that replaces this one's type takes over.</summary>
    internal System.Collections.Generic.IEnumerable<global::app.type.kind.@this> Kinds
        => Format is { } format ? Held.Prepend(format) : Held;

    /// <summary>The type itself when it answers to <paramref name="name"/> (its own MIME or extension), else one
    /// of its kinds that does — by name, alias, MIME or extension; null when none does.</summary>
    public override global::app.type.kind.@this? this[string name]
    {
        get
        {
            Ready();
            if (Names(name)) return this;
            // as Names reads a key: a key written with its dot is an extension, and only an extension answers it
            // (`.template` is a file's, never a kind named template); any other as it is, as a MIME without its
            // parameters, else as an extension
            var semicolon = name.IndexOf(';');
            lock (_gate)
            {
                Keyed();
                return name.StartsWith('.') ? _extensions?.GetValueOrDefault(name[1..])
                       : _keys?.GetValueOrDefault(name)
                         ?? (semicolon >= 0 ? _keys?.GetValueOrDefault(name[..semicolon].Trim()) : null)
                         ?? _extensions?.GetValueOrDefault(name);
            }
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
        => new(Held.Select(k => context.App.type.list[new global::app.type.@this(_owner.Name, k.Name), context]),
            context.App.type.list[typeof(global::app.type.kind.list.@this)]);

    /// <summary>Adds a kind of this type; a kind of the same name replaces the one before it. The type's own
    /// format (the empty name) is held as the type's, not as a kind.</summary>
    internal void Add(global::app.type.kind.@this kind)
    {
        lock (_gate)
        {
            if (kind.IsEmpty) { _format = kind; return; }
            // a kind of the same name is replaced; the keys are made again at the next look
            for (var at = _kinds.Count - 1; at >= 0; at--)
                if (string.Equals(_kinds[at].Name, kind.Name, System.StringComparison.OrdinalIgnoreCase))
                    _kinds.RemoveAt(at);
            _kinds.Add(kind);
            _keys = null;
            _extensions = null;
        }
    }

    // Makes the keys of the held kinds, when they are not made (under the gate): the first kind that answers keeps a key.
    private void Keyed()
    {
        if (_keys != null || _kinds.Count == 0) return;
        int keys = 0, extensions = 0;
        foreach (var kind in _kinds)
        {
            var (k, e) = kind.Count;
            keys += k;
            extensions += e;
        }
        var keyed = new System.Collections.Generic.Dictionary<string, global::app.type.kind.@this>(keys, System.StringComparer.OrdinalIgnoreCase);
        var extended = new System.Collections.Generic.Dictionary<string, global::app.type.kind.@this>(extensions, System.StringComparer.OrdinalIgnoreCase);
        foreach (var kind in _kinds) kind.Key(keyed, extended);
        _extensions = extended;
        _keys = keyed;
    }
}
