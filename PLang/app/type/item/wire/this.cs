namespace app.type.item.wire;

/// <summary>
/// A still-encoded slice of a larger document — raw text in the CAPTURE's encoding (a
/// <c>.pr</c> value slot), born holding the serializer that captured it. The capture passes
/// ITSELF; a wire never names a format — PLang stays serializer-independent (a bson <c>.pr</c>
/// slices bson; nothing here changes). Materializes through that serializer's reader; writes
/// back verbatim, byte-identical, so an untouched relay's signature still verifies. A string
/// TOKEN is decoded to bare content at capture (a plain <see cref="global::app.type.item.source"/>);
/// only a structured/number/bool/quoted slice rides here as its raw document bytes.
/// (Unrelated to the input/output channels.) Stores no context: decoding is a use, and the one
/// using it passes theirs.
/// </summary>
public sealed class @this : global::app.type.item.source
{
    // The serializer that sliced this raw — an object reference, never a format name. Held
    // since birth; the read reaches it directly (the registry lookup is what died, not the door).
    private readonly global::app.channel.serializer.ITransport _reader;

    public @this(string slice, global::app.type.@this type, global::app.channel.serializer.ITransport reader)
        : base(slice, type)
        => _reader = reader ?? throw new System.ArgumentNullException(nameof(reader));

    private protected override global::app.type.item.@this Read(actor.context.@this context)
        => _reader.Read(this, new global::app.type.reader.ReadContext(context, Type.Template));

    // The decoded value, with the caller's context: the kind owns the decode (one Parse, the same
    // value Value() materializes to); a kind that declines (csv, png) falls to the type reader.
    private global::app.type.item.@this Decoded(actor.context.@this context)
        => (Type.Kind is { } k ? context.App.Type.Kind[k.Name].Parse(Raw, context) : null) ?? Read(context);

    // A wire writes verbatim ONLY into its own format (a byte-identical relay of the captured
    // slice). Any other writer is a USE — decoding needs a context, which this context-free door
    // does not have: a foreign writer reaches a wire through Output.
    public override void Write(global::app.channel.serializer.IWriter w)
    {
        if (_reader.Owns(w)) { w.Raw((string)Raw); return; }
        throw new System.InvalidOperationException(
            "an undecoded wire writes into a foreign format through Output(writer, mode, context) — decoding needs the caller's context.");
    }

    // Output is the SHAPE-AWARE door. A wire wrapping a STRUCTURE (dict/list/object) reports
    // IsLeaf=true yet decodes to a non-leaf; Output lets the decoded value render itself through its
    // OWN shape (a leaf via Write, a structure structurally). Own format still rides raw —
    // byte-identical relay, signatures hold.
    public override async System.Threading.Tasks.ValueTask Output(
        global::app.channel.serializer.IWriter writer, global::app.View mode,
        global::app.actor.context.@this? context)
    {
        if (_reader.Owns(writer)) { writer.Raw((string)Raw); return; }
        if (context is null) throw new System.InvalidOperationException(
            "an undecoded wire writes into a foreign format only with the caller's context.");
        await Decoded(context).Output(writer, mode, context);
    }

    // Lowering an undecoded wire to CLR is a USE: graduate to the decoded value first (never hand
    // the ENCODED slice to a converter — the bug the inherited source.Clr would commit), then that
    // value lowers itself (a clr(json) → its kind's reflection read). The decode needs the asking
    // Data's context — the Data door passes it.
    internal override object? Clr(System.Type target, actor.context.@this? context)
        => context is null ? Clr(target) : Decoded(context).Clr(target);

    internal override object? Clr(System.Type target) => throw new System.InvalidOperationException(
        "an undecoded wire lowers to CLR through its Data (Data.Clr) — decoding needs the caller's context.");

    internal override global::app.type.item.source Declared(global::app.type.@this type)
        => new @this((string)Raw, type, _reader);
}
