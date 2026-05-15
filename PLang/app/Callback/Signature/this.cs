namespace app.Callback.Signature;

/// <summary>
/// Signature config for the callback subsystem. Read by <see cref="Data.@this"/>'s lazy
/// signature getter when the wrapped value is an <see cref="ICallback"/>.
/// </summary>
public sealed class @this
{
    /// <summary>
    /// Default signature lifetime for callback envelopes. <c>null</c> means
    /// no expiry (the integrity guarantee is unconditional). Serialised as
    /// ISO 8601 duration on the wire (e.g. <c>"PT5M"</c>) via
    /// <see cref="app.Channels.Serializers.TimeSpanIso8601"/>.
    /// </summary>
    public TimeSpan? Expires { get; set; }
}
