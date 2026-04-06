using App;
using App.Variables;

namespace App.modules.identity;

/// <summary>
/// Represents a PLang identity (key pair with metadata).
/// Data subclass — lives on Variables, navigable via %MyIdentity.PublicKey%.
/// Persistence is owned by IIdentityProvider.
/// </summary>
public sealed class Identity : Data.@this
{
    public Identity() : base("Identity") { }
    public Identity(string name) : base(name) { }

    /// <summary>Base64-encoded Ed25519 public key. Used as the identity in signed envelopes.</summary>
    public string PublicKey { get; set; } = "";

    /// <summary>Base64-encoded Ed25519 private key. Marked [Sensitive] — excluded from output serialization.</summary>
    [Sensitive]
    public string PrivateKey { get; set; } = "";

    /// <summary>Whether this is the active default identity for the system actor.</summary>
    public bool IsDefault { get; set; }

    /// <summary>Whether this identity has been soft-deleted. Archived identities are excluded from list results.</summary>
    public bool IsArchived { get; set; }

    /// <summary>When this identity was created (UTC).</summary>
    public DateTimeOffset Created { get; set; }

    /// <summary>
    /// String context returns the public key — %MyIdentity% in a string gives the public key.
    /// </summary>
    public override string ToString() => PublicKey;

    public override Data.@this Clone()
    {
        var clone = new Identity(Name)
        {
            PublicKey = PublicKey,
            PrivateKey = PrivateKey,
            IsDefault = IsDefault,
            IsArchived = IsArchived,
            Created = Created,
            Properties = Properties.Clone()
        };
        clone.Error = Error;
        clone.Handled = Handled;
        clone.Warnings = Warnings != null ? new List<Info>(Warnings) : null;
        clone.Signature = Signature;
        clone.Context = Context;
        return clone;
    }
}
