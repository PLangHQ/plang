using PLang.Interfaces;

namespace PLang.Utils;

internal class RSAKey
{
    private readonly ISettings settings;
    private readonly ISettingsRepository settingsRepository;

    public RSAKey(ISettings settings, ISettingsRepository settingsRepository)
    {
        this.settings = settings;
        this.settingsRepository = settingsRepository;
    }

    public record EncryptionKeys(string PublicKey, string PrivateKey, bool IsDefault);
}