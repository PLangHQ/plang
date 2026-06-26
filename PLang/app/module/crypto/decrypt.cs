namespace app.module.crypto;

/// <summary>
/// Symmetric decryption v1 — identity pass-through. Sibling of <see cref="encrypt"/>;
/// see its docs for the v1 contract.
/// </summary>
[Action("decrypt", Cacheable = false)]
public partial class decrypt : IContext
{
    /// <summary>The bytes to decrypt.</summary>
    [IsNotNull]
    public partial data.@this<global::app.type.binary.@this> Input { get; init; }

    public async Task<data.@this<global::app.type.binary.@this>> Run() =>
        Context.Ok<global::app.type.binary.@this>(await Input.Value());
}
