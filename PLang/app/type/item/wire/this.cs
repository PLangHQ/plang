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
    private readonly global::app.channel.serializer.ISerializer _reader;

    public @this(string slice, global::app.type.@this type, actor.context.@this context,
        global::app.channel.serializer.ISerializer reader)
        : base(slice, type, context)
        => _reader = reader ?? throw new System.ArgumentNullException(nameof(reader));

    private protected override global::app.type.item.@this Read()
        => _reader.Read(this, new global::app.type.reader.ReadContext(Context, Type.Template));

    // Verbatim — already document text, rides inline unquoted, byte-identical.
    public override void Write(global::app.channel.serializer.IWriter w) => w.Raw((string)Raw);

    internal override global::app.type.item.source Declared(global::app.type.@this type)
        => new @this((string)Raw, type, Context, _reader);
}
