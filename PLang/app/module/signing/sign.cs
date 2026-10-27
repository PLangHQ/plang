using app.module.signing.code;

namespace app.module.signing;

/// <summary>
/// Signs data using the configured signing provider.
/// </summary>
[Action("sign", Cacheable = false)]
public partial class sign : IContext
{
    /// <summary>The data to sign — as it is: a failed result is signed with its error, like any Data.</summary>
    [IsInitiated, Whole]
    public partial data.@this? Data { get; init; }

    /// <summary>Contracts for this signature. Default: ["C0"].</summary>
    public partial data.@this<global::app.type.item.list.@this>? Contracts { get; init; }

    /// <summary>Optional TTL. If set, signature.Expires = Created + this duration.
    /// Wire form is ISO 8601 (e.g. <c>"PT5M"</c>) via the global TimeSpan converter.</summary>
    public partial data.@this<global::app.type.item.duration.@this>? Expires { get; init; }

    /// <summary>The view the data is hashed in, so it matches the bytes the verifier re-hashes — handed whole by C# that
    /// knows it (the serializer's sign-if-missing, the view it writes in; a snapshot, its own); no step gives it. None:
    /// what crosses to others (Out).</summary>
    internal global::app.View View { get; init; } = global::app.View.Out;

    [Code]
    public partial ISigning Signer { get; }

    public async Task<data.@this> Start() => await Signer.SignAsync(this);
}
