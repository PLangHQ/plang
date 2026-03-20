using PLang.Runtime2.Engine.Context;
using PLang.Runtime2.Engine.Errors;
using PLang.Runtime2.Engine.Memory;
using PLang.Runtime2.modules.identity;
using PLangEngine = PLang.Runtime2.Engine.@this;

namespace PLang.Tests.Runtime2.Modules.signing;

/// <summary>
/// Tests identity create delegation to IKeyProvider.
/// When identity module creates keys, it delegates to the signing provider's
/// IKeyProvider interface instead of using internal KeyGenerator directly.
/// </summary>
public class IdentityKeyProviderTests
{
    private string _tempDir = null!;
    private PLangEngine _engine = null!;

    [Before(Test)]
    public void Setup()
    {
        _tempDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "plang_test_keyprovider_" + Guid.NewGuid().ToString("N")[..8]);
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
    public async Task Create_UsesKeyProviderFromRegistry()
    {
        // Mock IKeyProvider → identity gets mock's keys.
        //
        // Arrange: register MockKeyProvider that returns known keys
        // Act: create identity
        // Assert: identity.PublicKey == mock's known public key
        await Assert.Fail("stub — implementation depends on IKeyProvider delegation");
    }

    [Test]
    public async Task Create_DefaultEd25519_WhenNoOverride()
    {
        // No override → Ed25519 used (default behavior).
        //
        // Arrange: fresh engine, no provider override
        // Act: create identity
        // Assert: keys are valid Ed25519 (32 bytes each)
        await Assert.Fail("stub — implementation depends on IKeyProvider delegation");
    }

    [Test]
    public async Task Create_KeyProvider_Throws_ReturnsError()
    {
        // Provider throws → Data.FromError().
        //
        // Arrange: register ThrowingMockProvider as IKeyProvider
        // Act: create identity
        // Assert: result.Success == false, result.Error is not null
        await Assert.Fail("stub — implementation depends on IKeyProvider delegation");
    }

    [Test]
    public async Task Create_StoresKeysFromProvider()
    {
        // Mock returns known keys → stored exactly.
        //
        // Arrange: register MockKeyProvider with specific public/private keys
        // Act: create identity
        // Assert: loaded identity has exact keys from mock
        await Assert.Fail("stub — implementation depends on IKeyProvider delegation");
    }

    [Test]
    public async Task Create_WithProviderParam_UsesNamedProvider()
    {
        // provider="mock" → mock called.
        //
        // Arrange: register "mock" IKeyProvider and default Ed25519
        // Act: create identity with provider="mock"
        // Assert: identity keys match mock's output, not Ed25519
        await Assert.Fail("stub — implementation depends on IKeyProvider delegation");
    }
}
