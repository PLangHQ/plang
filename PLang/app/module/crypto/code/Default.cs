using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Nethereum.Util;
using app.error;
using app.variable;

namespace app.module.crypto.code;

public class Default : ICrypto
{
    public string Name => "default";
    public bool IsDefault { get; set; }
    public bool IsBuiltIn { get; set; }
    public string? Source { get; set; }

    public async Task<data.@this<global::app.module.crypto.type.hash.@this>> Hash(Hash action)
    {
        var data = action.Data;
        byte[] bytes;
        // Peek, never the value door: hashing is a courier read — opening the
        // door would parse + narrow the value mid-sign, making the signed
        // shape diverge from the wire/verify shape.
        var value = data.Peek();
        // A null/absent value has nothing to hash — the digest would be of the
        // empty wire shape, which silently verifies against any other empty.
        // Surface the missing input instead.
        if (value is null || await value.IsEmpty())
            return action.Context.Error<global::app.module.crypto.type.hash.@this>(new ActionError(
                "Hash requires a value to hash", "ValueRequired", 400));
        if (value is global::app.type.binary.@this bin)
        {
            bytes = bin.Value;
        }
        else
        {
            // Canonicalize through the same wire options the merged application/plang
            // serializer uses, so hashed-bytes ≡ wire-bytes (minus the outermost
            // Signature field, suppressed via Wire.MarkOuterForHash).
            // The outer signature transitively binds inner Datas' signatures.
            //
            // If something other than the canonical plang.@this is registered for
            // "application/plang" (custom transport, test double, future format),
            // fail loud — hash and wire would diverge silently and signature
            // verification would behave inconsistently across the same payload.
            var registered = action.Context?.Actor?.Channel.Serializers.GetByType("application/plang");
            if (registered != null && registered is not global::app.channel.serializer.plang.@this)
                return action.Context.Error<global::app.module.crypto.type.hash.@this>(new ActionError(
                    "Registered application/plang serializer is not the canonical plang.@this; hash bytes would diverge from wire bytes.",
                    "SerializerMismatch", 500));
            var serializer = (registered as global::app.channel.serializer.plang.@this)
                             ?? new global::app.channel.serializer.plang.@this(action.Context);
            // The value writes its OWN canonical bytes via data.Output — deterministic (fixed
            // key order, entries insertion-order); sign and verify both run it, so they agree
            // regardless of the wire format. View.Out omits the binding name (hash is name-
            // independent), and data.Output never emits a signature, so there's nothing to
            // suppress — MarkOuterForHash is unnecessary in the layer model.
            // TODO: serialize-to-MemoryStream-then-hash is the wrong shape — data.Output
            // should produce its hash intrinsically (write into a hashing writer), not via an
            // intermediate buffer. Correct behaviour, wrong means.
            using var hashStream = new MemoryStream();
            await using (var utf8 = new System.Text.Json.Utf8JsonWriter(hashStream))
            {
                var writer = new global::app.channel.serializer.json.Writer(
                    utf8, serializer.OutboundOptions, global::app.View.Out,
                    action.Context?.App?.Type?.Renderers, emitsSchema: true);
                await data.Output(writer, global::app.View.Out, action.Context, layer: true);
            }
            bytes = hashStream.ToArray();
        }
        string algorithm = (await action.Algorithm.Value())!.ToString()!.ToLowerInvariant();
        byte[]? hashBytes = algorithm switch
        {
            "keccak256" => new Sha3Keccack().CalculateHash(bytes),
            "sha256" => SHA256.HashData(bytes),
            _ => null
        };

        if (hashBytes == null)
            return action.Context.Error<global::app.module.crypto.type.hash.@this>(new ActionError($"Algorithm '{action.Algorithm.Peek()}' is not supported", "UnsupportedAlgorithm", 400));

        // The value IS a hash (a digest that knows its algorithm), not bare
        // bytes — so the builder annotates the write-to variable as `%x% (hash)`
        // and the live serializer renders the digest. The algorithm is the
        // value's KIND; stamp {name: hash, kind: <algorithm>} so verify reads
        // the algorithm off the value instead of a loose, mismatch-prone param.
        return action.Context.Ok<global::app.module.crypto.type.hash.@this>(new global::app.module.crypto.type.hash.@this(hashBytes, algorithm),
            global::app.type.@this.Create("hash", kind: algorithm));
    }

    public async Task<data.@this<global::app.type.@bool.@this>> Verify(Verify action)
    {
        // The expected hash and its algorithm. The digest's own kind is
        // authoritative — a sha256 hash can only be verified by recomputing
        // sha256. When `%hash%` binds an actual hash value, the algorithm rides
        // on it (no separate parameter); when it's a bare base64 string, the
        // kind on the Type (if any) or the Algorithm parameter supplies it.
        // The value under verification must exist before we bother parsing the
        // expected-hash string — a null payload is a missing input, not a bad hash.
        var toVerify = action.Data.Peek();
        if (toVerify is null || await toVerify.IsEmpty())
            return action.Context.Error<global::app.type.@bool.@this>(new ActionError(
                "Verify requires a value to verify", "ValueRequired", 400));

        global::app.module.crypto.type.hash.@this expected;
        string algorithm;
        if (await action.Hash.Value() is global::app.module.crypto.type.hash.@this bound)
        {
            expected = bound;
            algorithm = bound.Algorithm;
        }
        else
        {
            var hashKind = action.Hash.Type is { Name: "hash", Kind: { Length: > 0 } k } ? k : null;
            algorithm = hashKind ?? (await action.Algorithm.Value())!.Clr<string>()!;
            // The hash type owns base64↔byte parsing (OBP) — Verify doesn't
            // reach for Convert.FromBase64String / SequenceEqual itself.
            try { expected = global::app.module.crypto.type.hash.@this.FromBase64((await action.Hash.Value())?.ToString() ?? "", algorithm); }
            catch (FormatException) { return action.Context.Error<global::app.type.@bool.@this>(new ActionError("Hash string is not valid base64", "InvalidHash", 400)); }
        }

        // Recompute through crypto.hash so the algorithm switch stays in one
        // place (no forked digest logic here).
        var hashResult = await Hash(new Hash
        {
            Context = action.Context,
            Data = action.Data,
            Algorithm = new global::app.data.@this<global::app.type.text.@this>("Algorithm", algorithm, context: action.Context),
        });
        if (!hashResult.Success) return action.Context.Error<global::app.type.@bool.@this>(hashResult.Error!);

        return action.Context.Ok<global::app.type.@bool.@this>(((global::app.module.crypto.type.hash.@this)(await hashResult.Value())!).DigestEquals(expected));
    }
}
