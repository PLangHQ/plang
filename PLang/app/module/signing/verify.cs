using app.module.signing.code;

namespace app.module.signing;

/// <summary>
/// Verifies a signed Data.
/// </summary>
[Action("verify", Cacheable = false)]
public partial class verify : IContext
{
    /// <summary>The signed data to verify — as it is: a failed result signed with its error is verified like any Data.</summary>
    [Whole]
    public partial data.@this? Data { get; init; }

    /// <summary>Required contracts for verification.</summary>
    public partial data.@this<global::app.type.item.list.@this>? Contracts { get; init; }

    [Code]
    public partial ISigning Signer { get; }

    public async Task<data.@this<global::app.type.item.@bool.@this>> Start() => await Signer.VerifyAsync(this);
}
