using App.Config;

namespace App.modules.signing;

/// <summary>
/// Module settings for signing. Provider name and timeout for verification.
/// PLang: - set signing provider to 'ed25519'
/// PLang: - set signing timeout to 5 min
/// </summary>
public partial class Config : IConfig
{
    public string Provider { get; set; } = "ed25519";
    public long TimeoutMs { get; set; } = 300_000; // 5 minutes
}
