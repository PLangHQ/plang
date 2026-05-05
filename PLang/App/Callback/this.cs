namespace App.Callback;

/// <summary>
/// App-scoped configuration holder for the callback subsystem. Reachable as <c>app.Callback</c>.
/// NOT an <see cref="ICallback"/> — this is the config @this for the callback feature, not a
/// callback instance. Two distinct things share the word "Signature":
///   - <c>Data.@this.Signature</c> — wire envelope (the cryptographic seal on a Data payload)
///   - <c>app.Callback.Signature.ExpiresInMs</c> — config (default expiry seeded onto callbacks
///     when <c>Data.Signature</c> is lazily populated for an <see cref="ICallback"/> value)
/// </summary>
public sealed class @this
{
    /// <summary>Signature-related config — currently just <see cref="Signature.@this.ExpiresInMs"/>.</summary>
    public Signature.@this Signature { get; } = new();
}
