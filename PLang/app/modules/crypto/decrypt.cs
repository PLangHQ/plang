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
    public partial data.@this<byte[]> Input { get; init; }

    public Task<data.@this> Run() =>
        Task.FromResult(global::app.data.@this.Ok(Input.Value));
}
