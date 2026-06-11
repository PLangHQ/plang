namespace app.module.crypto;

/// <summary>
/// Symmetric encryption v1 — identity pass-through. The wiring is real (Callback's
/// Serialize calls through this action) so when the real implementation lands (tracked
/// in Documentation/Runtime2/todos.md) only this body changes. Async signature even
/// though v1 returns immediately — real impl will be async (key access, hardware modules).
/// </summary>
[Action("encrypt", Cacheable = false)]
public partial class encrypt : IContext
{
    /// <summary>The bytes to encrypt.</summary>
    [IsNotNull]
    public partial data.@this<global::app.type.binary.@this> Input { get; init; }

    public async Task<data.@this<global::app.type.binary.@this>> Run() =>
        global::app.data.@this<global::app.type.binary.@this>.Ok(await Input.Value());
}
