namespace app.type.format.item;

using TokenKind = global::app.type.format.TokenKind;

/// <summary>
/// A plang value read as tokens — the reader over a value in hand, beside <see cref="value.Reader"/> (a C# value's).
/// It walks the value by what each value says of itself: a leaf is the token its raw face is, a sequence an array of
/// its items, any other value an object of its members, each pair as the value enumerates it. A type's reader reads a
/// value of another type through it the way it reads one from its json, with no json between.
///
/// <para>A value whose members are itself (a single value that is neither a leaf nor a container) has no tokens and
/// is refused, never walked forever. A plang value has no raw bytes: <see cref="RawValue"/> refuses.</para>
/// </summary>
public struct Reader : IReader
{
    private readonly global::app.actor.context.@this _context;
    // the value the cursor is on; the container itself once its end is reached
    private global::app.type.item.@this _at;
    // the containers open around the cursor, innermost on top
    private readonly System.Collections.Generic.Stack<Open> _open;

    /// <summary>A reader over <paramref name="value"/>, its members read for <paramref name="context"/>.</summary>
    public Reader(global::app.type.item.@this value, global::app.actor.context.@this context)
    {
        _at = value;
        _context = context;
        _open = new();
    }

    // a container the cursor is inside: it, and where its items are
    private sealed class Open(global::app.type.item.@this container,
        System.Collections.Generic.IEnumerator<(global::app.data.@this key, global::app.data.@this value)> items)
    {
        public global::app.type.item.@this Container { get; } = container;
        public System.Collections.Generic.IEnumerator<(global::app.data.@this key, global::app.data.@this value)> Items { get; } = items;
    }

    public TokenKind Peek() => _at.IsLeaf ? Leaf.Peek() : _at.IsSequence ? TokenKind.Array : TokenKind.Object;

    // a leaf's token is its raw face's: the scalar reader reads it
    private value.Reader Leaf => new(_at.Backing!);

    public bool Null() => _at.IsLeaf && Leaf.Null();
    public bool Bool() => Leaf.Bool();
    public int Int() => Leaf.Int();
    public long Long() => Leaf.Long();
    public object Number() => Leaf.Number();
    public float Float() => Leaf.Float();
    public double Double() => Leaf.Double();
    public decimal Decimal() => Leaf.Decimal();
    public string String() => Leaf.String();
    public byte[] Bytes() => Leaf.Bytes();
    public System.DateTime DateTime() => Leaf.DateTime();
    public System.DateTimeOffset DateTimeOffset() => Leaf.DateTimeOffset();
    public System.TimeSpan TimeSpan() => Leaf.TimeSpan();
    public System.Guid Guid() => Leaf.Guid();

    public void BeginArray() => Enter();
    public bool NextElement() => Next(out _);
    public void EndArray() => Leave();

    public void BeginObject() => Enter();
    public bool NextName(out string name)
    {
        var more = Next(out var key);
        name = key;
        return more;
    }
    public void EndObject() => Leave();

    // a value not entered is passed over by the next step: nothing to skip
    public void Skip() { }

    public byte[] RawValue() => throw new global::app.error.AppException(new global::app.error.@this(
        "a plang value has no raw bytes — it reads as tokens", "NotTokens", 400));

    private void Enter() => _open.Push(new Open(_at, _at.EnumerateItems(_context).GetEnumerator()));

    private bool Next(out string key)
    {
        var open = _open.Peek();
        if (!open.Items.MoveNext())
        {
            key = "";
            _at = open.Container;
            return false;
        }
        var (name, value) = open.Items.Current;
        key = name.Peek().ToString() ?? "";
        _at = value.Peek();
        if (ReferenceEquals(_at, open.Container))
            throw new global::app.error.AppException(new global::app.error.@this(
                $"a {open.Container.Type.Name} reads as tokens only as a value, a list or a dict — it holds no members of its own",
                "NotTokens", 400));
        return true;
    }

    private void Leave() => _at = _open.Pop().Container;
}
