namespace app.modules.crypto;

/// <summary>
/// Symmetric decryption v1 — identity pass-through. Sibling of <see cref="encrypt"/>;
/// see its docs for the v1 contract.
/// </summary>
[System.ComponentModel.Description("Decrypt input bytes — v1 returns input unchanged")]
[Action("decrypt", Cacheable = false)]
public partial class decrypt : IContext
{
    /// <summary>The bytes to decrypt.</summary>
    [IsNotNull]
    public partial Data.@this<byte[]> Input { get; init; }

    public Task<Data.@this> Run() =>
        Task.FromResult(global::app.Data.@this.Ok(Input.Value));
}
