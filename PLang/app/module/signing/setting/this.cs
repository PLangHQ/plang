namespace app.module.signing.setting;

/// <summary>
/// The signing module's own settings — <c>%!signing.setting.expiry%</c>: how a signature read live is judged.
/// </summary>
public sealed class @this : global::app.type.item.setting.module.@this
{
    /// <summary>How long a signature read live off the wire is good after it was made — its expiry, unless what
    /// the signer signed ends it sooner. A signature read from plang's own store gets no such window.</summary>
    [Out, Store] public global::app.type.item.duration.@this Expiry { get; set; } = System.TimeSpan.FromMinutes(5);
}
