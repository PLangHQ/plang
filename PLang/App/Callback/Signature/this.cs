namespace App.Callback.Signature;

/// <summary>
/// Signature config for the callback subsystem. Read by <see cref="Data.@this"/>'s lazy
/// signature getter when the wrapped value is an <see cref="ICallback"/>.
/// </summary>
public sealed class @this
{
    /// <summary>
    /// Default signature lifetime in milliseconds for callback envelopes. <c>null</c> means
    /// no expiry (the integrity guarantee is unconditional). PLang shorthand
    /// <c>- set callback timeout to 5 minutes</c> writes 300000 here (Stage 4).
    /// </summary>
    public int? ExpiresInMs { get; set; }
}
