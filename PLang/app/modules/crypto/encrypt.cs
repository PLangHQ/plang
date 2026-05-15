namespace app.modules.crypto;

/// <summary>
/// Symmetric encryption v1 — identity pass-through. The wiring is real (Callback's
/// Serialize calls through this action) so when the real implementation lands (tracked
/// in Documentation/Runtime2/todos.md) only this body changes. Async signature even
/// though v1 returns immediately — real impl will be async (key access, hardware modules).
/// </summary>
[ModuleDescription("Symmetric encryption (v1: identity pass-through)")]
[System.ComponentModel.Description("Encrypt input bytes — v1 returns input unchanged")]
[Action("encrypt", Cacheable = false)]
public partial class encrypt : IContext
{
    /// <summary>The bytes to encrypt.</summary>
    [IsNotNull]
    public partial data.@this<byte[]> Input { get; init; }

    public Task<data.@this> Run() =>
        Task.FromResult(global::app.data.@this.Ok(Input.Value));
}
