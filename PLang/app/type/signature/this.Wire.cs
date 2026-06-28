using System.Text.Json;

namespace app.type.signature;

using text = global::app.type.text.@this;
using datetime = global::app.type.datetime.@this;
using binary = global::app.type.binary.@this;
using hash = global::app.module.crypto.type.hash.@this;

/// <summary>
/// signature layer — wire reconstruction (<see cref="FromWire"/>) and the
/// canonical signing-bytes (<see cref="ToSigningBytes"/>) the signing module
/// signs/verifies over. The layer owns BOTH directions of its own bytes (OBP):
/// <see cref="@this.Write"/> renders it, FromWire reads it, ToSigningBytes is the
/// deterministic attestation surface — the module never reaches into wire shape.
/// </summary>
public sealed partial class @this
{
    /// <summary>
    /// The deterministic bytes the signature is computed over — the signed
    /// metadata in fixed order (NOT the wire shape: no <c>@schema</c>, no
    /// <c>signature</c>, no <c>value</c>; the inner value is bound via
    /// <see cref="Hash"/>). Sign and verify must produce identical bytes, so the
    /// field order and encoding are frozen here.
    /// </summary>
    internal byte[] ToSigningBytes()
    {
        using var ms = new System.IO.MemoryStream();
        using (var w = new Utf8JsonWriter(ms))
        {
            w.WriteStartObject();
            w.WriteString("type", Algorithm.ToString());
            w.WriteString("nonce", Nonce.ToString());
            w.WriteString("created", Created.Value.ToString("O", System.Globalization.CultureInfo.InvariantCulture));
            if (Expires is { } e) w.WriteString("expires", e.Value.ToString("O", System.Globalization.CultureInfo.InvariantCulture));
            else w.WriteNull("expires");
            w.WriteString("identity", Identity.ToString());
            w.WriteStartArray("contracts");
            foreach (var c in ContractStrings()) w.WriteStringValue(c);
            w.WriteEndArray();
            w.WriteStartObject("hash");
            w.WriteString("type", Hash.Algorithm);
            w.WriteString("value", Hash.ToBase64());
            w.WriteEndObject();
            w.WriteEndObject();
        }
        return ms.ToArray();
    }

    /// <summary>The contract strings in order (empty when none).</summary>
    public System.Collections.Generic.IEnumerable<string> ContractStrings()
        => Contracts == null
            ? System.Array.Empty<string>()
            : System.Linq.Enumerable.Select(Contracts.Items, d => d.Peek().ToString() ?? "");

    // The signature layer is READ by the @schema:signature reader (app/data/schema/signature.cs),
    // which streams the fields off the IReader and verifies — there is no DOM rebuild here. The
    // reader reuses SafeBase64 below for the hash + signature bytes.

    internal static byte[] SafeBase64(string? s)
    {
        if (string.IsNullOrEmpty(s)) return System.Array.Empty<byte>();
        try { return System.Convert.FromBase64String(s); }
        catch (System.FormatException) { return System.Array.Empty<byte>(); }
    }
}
