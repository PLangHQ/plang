namespace App.Callback;

/// <summary>
/// App-scoped configuration holder for the callback subsystem. Reachable as <c>app.Callback</c>.
/// Post stage 2a.7 the only remaining feature is <see cref="Signature.@this.Expires"/>
/// (default expiry seeded onto Data signatures when one is requested).
/// </summary>
public sealed class @this
{
    /// <summary>Signature-related config — currently just <see cref="Signature.@this.Expires"/>.</summary>
    public Signature.@this Signature { get; } = new();
}
