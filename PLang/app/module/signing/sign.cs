using app.variable;
using app.module.signing.code;

namespace app.module.signing;

/// <summary>
/// Signs data using the configured signing provider.
/// </summary>
[Action("sign", Cacheable = false)]
public partial class sign : global::app.type.item.@this, global::app.type.item.ICreate<sign>, IContext
{
    /// <summary>The data to sign.</summary>
    [IsInitiated]
    public partial data.@this? Data { get; init; }

    /// <summary>Contracts for this signature. Default: ["C0"].</summary>
    public partial data.@this<global::app.type.list.@this>? Contracts { get; init; }

    /// <summary>Optional headers to include in the signature object.</summary>
    public partial data.@this<global::app.type.dict.@this>? Headers { get; init; }

    /// <summary>Optional TTL. If set, signature.Expires = Created + this duration.
    /// Wire form is ISO 8601 (e.g. <c>"PT5M"</c>) via the global TimeSpan converter.</summary>
    public partial data.@this<global::app.type.duration.@this>? Expires { get; init; }

    /// <summary>
    /// Whether the data is being signed for the Store view (true) or transport/Out (false).
    /// The hash is taken in this view so it matches the wire bytes the verifier re-hashes —
    /// the serializer sets it from the view sign-if-missing fires in. Default false (Out).
    /// </summary>
    public partial data.@this<global::app.type.@bool.@this>? StoreView { get; init; }

    [Code]
    public partial ISigning Signer { get; }

    public async Task<data.@this> Run() => await Signer.SignAsync(this);
}
