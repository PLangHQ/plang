namespace app.type.item.wire.kind.plang;

/// <summary>
/// plang's own format — <c>application/plang</c>: content that is a whole Data (its value, properties and
/// signature), not a value. Its decode hands back that Data, read by the transport; every other format's
/// content is a value of its type.
/// </summary>
public sealed class @this : global::app.type.kind.@this
{
    public @this() : base("plang") { }

    protected internal override string Owner => "wire";

    public override System.Collections.Generic.IReadOnlyList<string> Mime => ["application/plang", "application/plang+json"];
    public override System.Collections.Generic.IReadOnlyList<string> Extension => [".plang"];
    public override bool Compressible => true;

    /// <summary>The Data the bytes are — read by the asker's transport, which reconstructs it whole.</summary>
    public override async System.Threading.Tasks.Task<global::app.data.@this> Decode(byte[] raw,
        global::app.actor.context.@this context, string name = "", System.Threading.CancellationToken ct = default)
    {
        var transport = context.Actor?.Channel.Serializers.Transport
            ?? throw new System.InvalidOperationException("plang content reached with no actor to read it — its transport is the actor's.");
        using var stream = new System.IO.MemoryStream(raw);
        return await transport.DeserializeAsync(stream, cancellationToken: ct);
    }
}
