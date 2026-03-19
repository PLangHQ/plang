using PLang.Runtime2.Engine;
using PLang.Runtime2.Engine.Memory;
using PLang.Runtime2.modules.crypto;
using PLang.Runtime2.modules.crypto.providers;
using PLang.SafeFileSystem;

namespace PLang.Tests.Runtime2.Modules.crypto;

public class ProviderResolutionTests : IDisposable
{
    private readonly string _tempDir;
    private readonly PLangFileSystem _fs;
    private readonly PLang.Runtime2.Engine.@this _engine;

    public ProviderResolutionTests()
    {
        _tempDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "plang_test_" + Guid.NewGuid().ToString("N"));
        System.IO.Directory.CreateDirectory(_tempDir);
        _fs = new PLangFileSystem(_tempDir, "");
        _engine = new PLang.Runtime2.Engine.@this(_tempDir, fileSystem: _fs);
    }

    public void Dispose()
    {
        _engine.DisposeAsync().AsTask().GetAwaiter().GetResult();
        if (System.IO.Directory.Exists(_tempDir))
            System.IO.Directory.Delete(_tempDir, true);
    }

    [Test]
    public async Task Hash_UsesProviderFromSettings_NotDefault()
    {
        // Register a mock provider that returns a known marker hash
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
    public async Task Hash_PerCallAlgorithm_OverridesDefault()
    {
        // Even if engine default is keccak256, passing algorithm="sha256"
        // per-call should produce SHA256 output.
        //
        // Arrange: engine with default provider
        // Act: hash action with explicit algorithm="sha256"
        // Assert: output matches known SHA256 hash, not Keccak256
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
        // Arrange: register mock provider via settings
        // Act: verify via action handler
        // Assert: mock provider's Verify was called (returns known value)
        await Assert.Fail("stub — implementation depends on crypto module");
    }
}
