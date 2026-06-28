namespace app.data.schema;

using Data = global::app.data.@this;

/// <summary>
/// Reads one <c>@schema</c> layer of a Data wire object into a Data — the Data-level mirror
/// of <see cref="app.type.reader.ITypeReader"/> (which reads a value). The writer always
/// emits <c>@schema</c> first; the registry (<see cref="@this"/>) dispatches on it: <c>data</c>
/// reads the <c>{name, type, value, properties}</c> envelope, <c>signature</c> verifies the
/// attestation layer and peels to the inner data.
///
/// <para><paramref name="options"/> is STJ plumbing for the sub-reads that still ride their own
/// JsonConverter (the goal.call TEMP, the signature layer's <c>FromWire</c>) — it drains as
/// those gain readers. <c>ReadContext</c> carries the actor context, authored template, and the
/// read <c>View</c> (the signature layer gates verify on it).</para>
/// </summary>
public interface ISchemaReader
{
    /// <summary>The <c>@schema</c> value this reader handles — the registry key (mirror of
    /// <see cref="app.type.reader.ITypeReader.Kind"/>).</summary>
    string Schema { get; }

    Data Read(ref global::app.channel.serializer.json.Reader reader,
        global::app.type.reader.ReadContext ctx,
        System.Text.Json.JsonSerializerOptions options);
}
