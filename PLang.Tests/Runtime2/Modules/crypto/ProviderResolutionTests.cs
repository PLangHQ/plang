using PLang.Runtime2.Engine.Context;
using PLang.Runtime2.Engine.Errors;
using PLang.Runtime2.Engine.Memory;
using PLang.Runtime2.modules.crypto;
using PLang.Runtime2.modules.crypto.providers;
using PLangEngine = PLang.Runtime2.Engine.@this;

namespace PLang.Tests.Runtime2.Modules.crypto;

public class ProviderResolutionTests
{
    private string _tempDir = null!;
    private PLangEngine _engine = null!;

    [Before(Test)]
    public void Setup()
    {
        _tempDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "plang_test_crypto_" + Guid.NewGuid().ToString("N")[..8]);
        System.IO.Directory.CreateDirectory(_tempDir);
        _engine = new PLangEngine(_tempDir);
    }

    [After(Test)]
    public void Cleanup()
    {
        try
        {
            _engine.DisposeAsync().AsTask().GetAwaiter().GetResult();
            if (System.IO.Directory.Exists(_tempDir))
                System.IO.Directory.Delete(_tempDir, true);
        }
        catch { /* best effort cleanup */ }
    }

    private PLangContext Ctx => _engine.System.Context;

    [Test]
    public async Task Hash_UsesProviderFromSettings_NotDefault()
    {
        // Register a mock ICryptoProvider that returns a known marker hash.
        // When settings point to this provider, the hash action should use it
        // instead of DefaultProvider.
        //
        // Arrange: set crypto.provider setting to "mock"
        // Register MockCryptoProvider that returns all-zero bytes
        // Act: hash via action handler
        // Assert: result matches mock output, not real Keccak256
        await Assert.Fail("stub — implementation depends on crypto module");
    }

    [Test]
    public async Task Hash_NoProviderConfigured_FallsToBuiltInDefault()
    {
        // No settings configured at all — should use DefaultProvider with keccak256.
        //
        // Arrange: fresh engine, no crypto settings
        // Act: hash action with no algorithm specified
        // Assert: output matches known Keccak256 hash
        await Assert.Fail("stub — implementation depends on crypto module");
    }

    [Test]
    public async Task Verify_UsesProviderFromSettings()
    {
        // Same as Hash_UsesProviderFromSettings but for the verify action.
        //
        // Arrange: register mock ICryptoProvider via settings
        // Act: verify via action handler
        // Assert: mock provider's Verify was called (returns known value)
        await Assert.Fail("stub — implementation depends on crypto module");
    }
}
