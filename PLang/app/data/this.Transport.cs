using app;
using app.error;

namespace app.data;

using type = global::app.type.@this;

/// <summary>
/// Data — transport-pipeline concern.
/// Pipeline methods: Encrypt (outbound) and Decrypt (inbound). Compression is the archive module's
/// (<c>archive.pack</c>/<c>archive.unpack</c>).
/// </summary>
public partial class @this
{
    // Signing is an I/O-boundary concern: a Data crossing the application/plang
    // boundary is wrapped in a `signature` layer at write (Wire.Write), peeled and
    // verified at read. Nothing in memory signs; a Data read in keeps the signature
    // it arrived in (Signature), never written out again.

    /// <summary>
    /// Encrypts and wraps the result as an encrypted outer. Requires a crypto
    /// service on App (not yet implemented). Returns self until crypto is available.
    /// Intended pattern: serialize to bytes, encrypt, wrap as
    /// Data { type = "encrypted", value = byte[] (cipher-of-serialized-Data) }.
    /// </summary>
    public @this Encrypt()
    {
        // Encryption requires a crypto service on App (not yet implemented).
        // When available: navigate through _context.App to the crypto handler,
        // serialize this Data to bytes, encrypt, wrap in the encrypted outer.
        return this;
    }

    // --- Inbound pipeline: Decrypt ---

    /// <summary>
    /// Decrypts an encrypted outer. If type is not "encrypted", returns self (no-op).
    /// Requires a crypto service on App (not yet implemented). Returns self until crypto is available.
    /// Intended pattern: read inner Data for algorithm + properties, decrypt bytes, deserialize result.
    /// </summary>
    public @this Decrypt()
    {
        if (!string.Equals(Type?.Name, "encrypted", StringComparison.OrdinalIgnoreCase))
            return this;

        // Decryption requires a crypto service on App (not yet implemented).
        // When available: navigate through _context.App to the crypto handler,
        // read inner Data (algorithm, keyId, nonce from Properties), decrypt, deserialize.
        return this;
    }
}
