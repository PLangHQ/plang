// === Data Envelope Pipeline — Output Example ===
// Shows how Data flows through Wrap → Compress → Encrypt on io.write

// --- Starting point: flat internal Data ---
var data = new Data("report", fileBytes, Type.FromMime("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"));

// data looks like:
// Data { Name = "report", Type = "application/vnd...sheet", Value = byte[148832] }


// === Step 1: Wrap() ===
// Sets the category envelope from TypeMapping

public Data Wrap()
{
    // Look up category from type string or file extension
    var category = TypeMapping.GetCategory(this.Type.Value); // → "spreadsheet"
    if (category == null)
        return this; // unknown type, no wrapping

    // Outer Data = routing envelope
    // Inner Data = this (the content)
    return new Data("", this, Type.FromName(category));
    // returns: Data { Type = "spreadsheet", Value = Data { Type = "application/vnd...sheet", Value = byte[] } }
}


// === Step 2: Compress() ===
// Compresses if the content is compressible, no-op otherwise

private static readonly HashSet<string> NotCompressible = 
    new(StringComparer.OrdinalIgnoreCase) { "image", "video", "audio", "archive" };

public Data Compress()
{
    // Check outer type (the category) for compressibility
    if (NotCompressible.Contains(this.Type.Value))
        return this; // no-op, return self unchanged

    // Serialize this entire Data (the wrapped envelope) to bytes
    var innerBytes = Serializer.Serialize(this); // JSON or binary

    // Compress the bytes
    var compressedBytes = GzipCompressor.Compress(innerBytes);

    // Build the inner Data (handler-specific)
    var inner = new Data("", compressedBytes, Type.FromName("gzip"));

    // Wrap in archived envelope
    return new Data("", inner, Type.FromName("archived"));
    // returns: Data { Type = "archived", Value = Data { Type = "gzip", Value = byte[compressed] } }
}


// === Step 3: Encrypt() ===
// Always encrypts, wraps in encrypted envelope

public Data Encrypt()
{
    // Serialize this entire Data to bytes
    var innerBytes = Serializer.Serialize(this);

    // Encrypt the bytes (key resolution from Identity/context)
    var (encryptedBytes, nonce) = Ed25519Crypto.Encrypt(innerBytes, recipientPublicKey);

    // Build the inner Data with algorithm-specific properties
    var inner = new Data("", encryptedBytes, Type.FromName("ed25519"));
    inner.Properties = new Properties
    {
        new Data("keyId", "did:plang:abc123"),
        new Data("nonce", nonce),
        new Data("sender", myPublicKey)
    };

    // Wrap in encrypted envelope
    return new Data("", inner, Type.FromName("encrypted"));
    // returns: Data { Type = "encrypted", Value = Data { Type = "ed25519", Value = byte[encrypted], Properties = [...] } }
}


// === The full chain ===

// In io.write:
var envelope = data      // Data { Type = "application/vnd...sheet", Value = byte[] }
    .Wrap()              // Data { Type = "spreadsheet", Value = Data { Type = "application/vnd...sheet", Value = byte[] } }
    .Compress()          // Data { Type = "archived", Value = Data { Type = "gzip", Value = compressedBytes } }
    .Encrypt();          // Data { Type = "encrypted", Value = Data { Type = "ed25519", Value = encryptedBytes, Properties = [...] } }

// Serialization adds Signature automatically
// envelope.Signature = sign(serialize(envelope))

await channel.WriteAsync(envelope);


// === Input: the reverse ===

// In io.read:
var received = await channel.ReadAsync(); // deserialization verifies Signature, sets Verified

var content = received   // Data { Type = "encrypted", Verified = true, Value = Data { Type = "ed25519", ... } }
    .Decrypt()           // Data { Type = "archived", Value = Data { Type = "gzip", Value = compressedBytes } }
    .Decompress()        // Data { Type = "spreadsheet", Value = Data { Type = "application/vnd...sheet", Value = byte[] } }
    .Unwrap();           // Data { Type = "application/vnd...sheet", Value = byte[] }

// Back to flat internal Data, ready for the runtime


// === Decrypt() ===

public Data Decrypt()
{
    if (!string.Equals(this.Type.Value, "encrypted", StringComparison.OrdinalIgnoreCase))
        return this; // not encrypted, no-op

    // Inner Data has the algorithm specifics
    var inner = this.Value as Data;
    if (inner == null)
        return Data.FromError(new Error("encrypted Data has no inner Data"));

    // Read algorithm-specific properties
    var algorithm = inner.Type.Value; // "ed25519"
    var keyId = inner.Properties?.Get("keyId")?.GetValue<string>();
    var nonce = inner.Properties?.Get("nonce")?.GetValue<byte[]>();

    // Decrypt
    var decryptedBytes = Ed25519Crypto.Decrypt(inner.GetValue<byte[]>(), nonce, senderPublicKey);

    // Deserialize back to Data (the next layer)
    return Serializer.Deserialize<Data>(decryptedBytes);
}


// === Decompress() ===

public Data Decompress()
{
    if (!string.Equals(this.Type.Value, "archived", StringComparison.OrdinalIgnoreCase))
        return this; // not compressed, no-op

    var inner = this.Value as Data;
    if (inner == null)
        return Data.FromError(new Error("archived Data has no inner Data"));

    var algorithm = inner.Type.Value; // "gzip"

    var decompressedBytes = GzipCompressor.Decompress(inner.GetValue<byte[]>());

    return Serializer.Deserialize<Data>(decompressedBytes);
}


// === Unwrap() ===

public Data Unwrap()
{
    // If value is a Data, unwrap it (strip the category envelope)
    if (this.Value is Data inner)
        return inner;

    return this; // already flat
}
