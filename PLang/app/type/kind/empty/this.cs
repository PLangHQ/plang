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
    // The kinds this type holds, in the order they came — replaced whole as a kind comes in, never written once
    // published: a type is shared by every app made from the same code, so a lookup reads it without the gate.
    private volatile global::app.type.kind.@this[] _kinds = [];
    // The held kinds by every key each answers to (Names): its name, its aliases, its MIMEs — and apart, by its
    // extensions without the dot — the first kind that answers, in the order they came; so a key finds its kind in one
    // look, never by asking each kind in turn. Made on the first look after kinds came in, from the kinds as they were
    // then, and made again only when they've changed since: a type taking its kinds one by one makes its tables once,
    // and one that holds no kind never makes them. Replaced whole, read without the gate.
    private volatile Tables? _tables;

    // The key tables of one set of kinds — the array they were made from, so a look knows they still answer it.
    private sealed record Tables(global::app.type.kind.@this[] From,
        System.Collections.Generic.Dictionary<string, global::app.type.kind.@this> Keys,
        System.Collections.Generic.Dictionary<string, global::app.type.kind.@this> Extensions)
    {
        internal static Tables Of(global::app.type.kind.@this[] kinds)
        {
            var keys = new System.Collections.Generic.Dictionary<string, global::app.type.kind.@this>(System.StringComparer.OrdinalIgnoreCase);
            var extensions = new System.Collections.Generic.Dictionary<string, global::app.type.kind.@this>(System.StringComparer.OrdinalIgnoreCase);
            foreach (var entry in kinds)
            {
                foreach (var key in entry.Keys) keys.TryAdd(key, entry);
                foreach (var extension in entry.Extension) extensions.TryAdd(extension.TrimStart('.'), entry);
            }
            return new(kinds, keys, extensions);
        }
    }

    // The tables of the kinds as an addition last published them — made now when they were made from others.
    private Tables Current
    {
        get
        {
            var kinds = _kinds;
            if (_tables is { } made && ReferenceEquals(made.From, kinds)) return made;
            lock (_gate)
            {
                kinds = _kinds;
                if (_tables is { } again && ReferenceEquals(again.From, kinds)) return again;
                return _tables = Tables.Of(kinds);
            }
        }
    }

    // Taken by an addition and by the making of the tables: one at a time.
    private readonly object _gate = new();
    // The type's own format — its class's [Format("", …)]: plain text is {text}, opaque bytes {binary}.
    private volatile global::app.type.kind.@this? _format;

    /// <summary>The empty kind of <paramref name="owner"/>, the type it is born by.</summary>
    public @this(global::app.type.@this owner, global::app.type.kind.@this? format = null) : base("")
    {
        _owner = owner;
        _format = format;
    }

    /// <summary>The empty kind is its own kind — ends the <c>kind : type</c> ctor's kind-minting chain
    /// without a new allocation. (Serializers are kept off <c>type.kind</c>, so this self-reference
    /// never loops a walk.)</summary>
    protected override global::app.type.kind.@this BornKind() => this;

    protected internal override global::app.type.@this Owner => _owner;

    public override System.Collections.Generic.IReadOnlyList<string> Mime => _format?.Mime ?? [];
    public override System.Collections.Generic.IReadOnlyList<string> Extension => _format?.Extension ?? [];
    public override bool Compressible => _format?.Compressible ?? false;
    public override bool IsText => _format?.IsText ?? false;

    /// <summary>The type's own format's view (a .pr's is Store); a type with none crosses to others.</summary>
    public override global::app.View View => _format?.View ?? base.View;

    /// <summary>The type's own format writes it (<c>.pr</c> for goal); a type with none writes nothing.</summary>
    public override System.Threading.Tasks.Task<global::app.data.@this> Encode(System.IO.Stream stream,
        global::app.data.@this data, global::app.actor.context.@this context, global::app.View view,
        System.Text.Encoding? encoding = null, System.Threading.CancellationToken ct = default)
        => _format != null ? _format.Encode(stream, data, context, view, encoding, ct)
            : base.Encode(stream, data, context, view, encoding, ct);

    /// <summary>The type's own format reads it (<c>.pr</c> for goal); a type with none has its content born lazily.</summary>
    public override System.Threading.Tasks.Task<global::app.data.@this> Decode(byte[] raw,
        global::app.actor.context.@this context, global::app.View view, string name = "",
        System.Threading.CancellationToken ct = default, global::app.type.item.template.kind.@this? template = null, global::app.type.item.path.@this? origin = null)
        => _format != null ? _format.Decode(raw, context, view, name, ct, template, origin)
            : base.Decode(raw, context, view, name, ct, template, origin);

    /// <summary>What a value of the type can be in <paramref name="step"/>: its own kind's answer when it has one (a
    /// conversation offers only what can continue one), else the step's variables.</summary>
    public override System.Threading.Tasks.ValueTask<System.Collections.Generic.IReadOnlyList<global::app.type.item.@this>> Offers(global::app.goal.step.@this step)
        => _format?.Offers(step) ?? base.Offers(step);

    /// <summary>Closed as the type's own kind is (a bool's); a type with none offers an open set.</summary>
    public override bool IsClosed => _format?.IsClosed ?? false;

    // The kinds this type holds, in the order they came, as an addition last published them.
    private global::app.type.kind.@this[] Held => _kinds;

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
            // as Names reads a key: a key written with its dot is an extension, and only an extension answers it
            // (`.template` is a file's, never a kind named template); any other as it is, as a MIME without its
            // parameters, else as an extension
            // a type that holds no kind answers no key, and makes no table
            if (_kinds.Length == 0) return null;
            var semicolon = name.IndexOf(';');
            // the tables of the kinds as they stand, read once
            var tables = Current;
            return name.StartsWith('.') ? tables.Extensions.GetValueOrDefault(name[1..])
                   : tables.Keys.GetValueOrDefault(name)
                     ?? (semicolon >= 0 ? tables.Keys.GetValueOrDefault(name[..semicolon].Trim()) : null)
                     ?? tables.Extensions.GetValueOrDefault(name);
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
            // the new kinds, made apart in one array, then published whole: a reader sees the kinds before or after,
            // never between; the tables are made again at the next look
            var held = _kinds;
            _kinds = System.Array.Exists(held, k => string.Equals(k.Name, kind.Name, System.StringComparison.OrdinalIgnoreCase))
                ? [.. System.Array.FindAll(held, k => !string.Equals(k.Name, kind.Name, System.StringComparison.OrdinalIgnoreCase)), kind]
                : [.. held, kind];
        }
    }
}
