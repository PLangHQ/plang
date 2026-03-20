using PLang.Runtime2.Engine.Settings;

namespace PLang.Runtime2.modules.signing;

/// <summary>
/// Module settings for signing. Provider name and timeout for verification.
/// </summary>
public partial class Settings : ISettings
{
    public string Provider { get; set; } = "ed25519";
    public long TimeoutMs { get; set; } = 300_000; // 5 minutes
}
