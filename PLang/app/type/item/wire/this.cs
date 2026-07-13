namespace app.type.item.wire;

/// <summary>
/// A still-encoded slice of a larger document — raw text in the CAPTURE's encoding (a
/// <c>.pr</c> value slot), born holding the serializer that captured it. The capture passes
/// ITSELF; a wire never names a format — PLang stays serializer-independent (a bson <c>.pr</c>
/// slices bson; nothing here changes). Materializes through that serializer's reader; writes
/// back verbatim, byte-identical, so an untouched relay's signature still verifies. A string
/// TOKEN is decoded to bare content at capture (a plain <see cref="global::app.type.item.source"/>);
/// only a structured/number/bool/quoted slice rides here as its raw document bytes.
/// (Unrelated to the input/output channels.)
/// </summary>
public sealed class @this : global::app.type.item.source
{
    // The serializer that sliced this raw — an object reference, never a format name. Held
    // since birth; the read reaches it directly (the registry lookup is what died, not the door).
    private readonly global::app.channel.serializer.ITransport _reader;

    public @this(string slice, global::app.type.@this type, actor.context.@this context,
        global::app.channel.serializer.ITransport reader)
        : base(slice, type, context)
        => _reader = reader ?? throw new System.ArgumentNullException(nameof(reader));

    private protected override global::app.type.item.@this Read()
        => _reader.Read(this, new global::app.type.reader.ReadContext(Context, Type.Template));

    // A wire writes verbatim ONLY into its own format (a byte-identical relay of the captured
    // slice); any other writer is a USE — the wire graduates to its decoded value and that writes
    // itself (a text writer gets bare content, not the quoted document slice). Strictness holds: a
    // mismatched slice throws at the materialize, output included — the birth site is the bug.
    public override void Write(global::app.channel.serializer.IWriter w)
    {
        if (_reader.Owns(w)) { w.Raw((string)Raw); return; }
        // Graduate to the decoded value: the kind owns the json decode (one Parse, the same value
        // Value() materializes to); a kind that declines (csv, png) falls to the type reader.
        var decoded = (Type.Kind is { } k ? Context.App.Type.Kind[k.Name].Parse(Raw, Context) : null) ?? Read();
        decoded.Write(w);
    }

    // Lowering an undecoded wire to CLR is a USE: graduate to the decoded value first (never hand
    // the ENCODED slice to a converter — the bug the inherited source.Clr would commit), then that
    // value lowers itself (a clr(json) → its kind's reflection read).
    internal override object? Clr(System.Type target)
    {
        var decoded = (Type.Kind is { } k ? Context.App.Type.Kind[k.Name].Parse(Raw, Context) : null) ?? Read();
        return decoded.Clr(target);
    }

    internal override global::app.type.item.source Declared(global::app.type.@this type)
        => new @this((string)Raw, type, Context, _reader);
}
