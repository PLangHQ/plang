using PLang.Runtime2.Engine;

namespace PLang.Runtime2.modules.identity;

/// <summary>
/// Represents a PLang identity (key pair with metadata).
/// Pure data class — persistence is owned by IIdentityProvider.
/// </summary>
public sealed class IdentityVariable
{
    public string Name { get; set; } = "";
    public string PublicKey { get; set; } = "";

    [Sensitive]
    public string PrivateKey { get; set; } = "";

    public bool IsDefault { get; set; }
    public bool IsArchived { get; set; }
    public DateTimeOffset Created { get; set; }

    /// <summary>
    /// String context returns the public key — %MyIdentity% in a string gives the public key.
    /// </summary>
    public override string ToString() => PublicKey;
}
