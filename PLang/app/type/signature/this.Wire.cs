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

    /// <summary>
    /// Reconstructs a signature layer from its wire object. The inner
    /// <c>value</c> is read back as the Data it is (through the same wire
    /// converter via <paramref name="options"/>); every other field maps to its
    /// born-native type. Verification is the caller's job (the read boundary runs
    /// the verify action) — FromWire only rebuilds.
    /// </summary>
    internal static @this FromWire(JsonElement el, JsonSerializerOptions options)
    {
        text algorithm = new("ed25519"), nonce = new(""), identity = new("");
        System.DateTimeOffset created = default;
        System.DateTimeOffset? expires = null;
        string hashAlgo = "keccak256", hashValue = "";
        binary sig = new(System.Array.Empty<byte>());
        global::app.type.list.@this? contracts = null;
        global::app.data.@this inner = global::app.data.@this.Ok((object?)null);

        foreach (var prop in el.EnumerateObject())
        {
            switch (prop.Name.ToLowerInvariant())
            {
                case "@schema": break;
                case "type": algorithm = new text(prop.Value.GetString() ?? "ed25519"); break;
                case "nonce": nonce = new text(prop.Value.GetString() ?? ""); break;
                case "created": created = prop.Value.GetDateTimeOffset(); break;
                case "expires":
                    expires = prop.Value.ValueKind == JsonValueKind.Null ? null : prop.Value.GetDateTimeOffset();
                    break;
                case "identity": identity = new text(prop.Value.GetString() ?? ""); break;
                case "contracts":
                {
                    var items = new System.Collections.Generic.List<global::app.data.@this>();
                    foreach (var c in prop.Value.EnumerateArray())
                        items.Add(global::app.data.@this.Ok(new text(c.GetString() ?? "")));
                    contracts = new global::app.type.list.@this(items);
                    break;
                }
                case "hash":
                    hashAlgo = prop.Value.TryGetProperty("type", out var ht) ? ht.GetString() ?? "keccak256" : "keccak256";
                    hashValue = prop.Value.TryGetProperty("value", out var hv) ? hv.GetString() ?? "" : "";
                    break;
                case "signature":
                    sig = new binary(SafeBase64(prop.Value.GetString()));
                    break;
                case "value":
                    inner = prop.Value.Deserialize<global::app.data.@this>(options)
                            ?? global::app.data.@this.Ok((object?)null);
                    break;
            }
        }

        return new @this(inner, algorithm, nonce, new datetime(created), identity,
            new hash(SafeBase64(hashValue), hashAlgo), sig,
            expires is { } ex ? new datetime(ex) : null, contracts);
    }

    private static byte[] SafeBase64(string? s)
    {
        if (string.IsNullOrEmpty(s)) return System.Array.Empty<byte>();
        try { return System.Convert.FromBase64String(s); }
        catch (System.FormatException) { return System.Array.Empty<byte>(); }
    }
}
