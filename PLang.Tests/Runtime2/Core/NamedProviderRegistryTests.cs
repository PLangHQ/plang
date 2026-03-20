using PLang.Runtime2.Engine.Context;
using PLang.Runtime2.Engine.Errors;
using PLang.Runtime2.Engine.Memory;
using PLangEngine = PLang.Runtime2.Engine.@this;

namespace PLang.Tests.Runtime2.Core;

/// <summary>
/// Tests the upgraded Engine.Providers named registry.
/// Instantiate Providers directly — no engine needed for most tests.
/// </summary>
public class NamedProviderRegistryTests
{
    private string _tempDir = null!;
    private PLangEngine _engine = null!;

    [Before(Test)]
    public void Setup()
    {
        _tempDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "plang_test_registry_" + Guid.NewGuid().ToString("N")[..8]);
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

    #region Register & Retrieve

    [Test]
    public async Task Register_SingleProvider_CanRetrieveByName()
    {
        // Register a named provider, retrieve by name — should return the same instance.
        //
        // Arrange: create MockSigningProvider with Name="ed25519"
        // Act: Register, then Get<ISigningProvider>("ed25519")
        // Assert: returned provider is the same instance, Name matches
        await Assert.Fail("stub — implementation depends on NamedProviderRegistry");
    }

    [Test]
    public async Task Register_FirstProvider_BecomesDefault()
    {
        // First registered provider auto-gets IsDefault = true.
        //
        // Arrange: create MockSigningProvider with Name="first"
        // Act: Register
        // Assert: provider.IsDefault == true
        await Assert.Fail("stub — implementation depends on NamedProviderRegistry");
    }

    [Test]
    public async Task Register_MultipleProviders_EachRetrievableByName()
    {
        // Two providers for same interface, both retrievable by their names.
        //
        // Arrange: Register "ed25519" and "ecdsa" providers
        // Act: Get by each name
        // Assert: both returned, each matching expected name
        await Assert.Fail("stub — implementation depends on NamedProviderRegistry");
    }

    [Test]
    public async Task Register_SecondProvider_DoesNotOverrideDefault()
    {
        // Second provider is NOT default (first keeps it).
        //
        // Arrange: Register "first" then "second"
        // Act: check IsDefault on both
        // Assert: first.IsDefault == true, second.IsDefault == false
        await Assert.Fail("stub — implementation depends on NamedProviderRegistry");
    }

    [Test]
    public async Task Register_DuplicateName_ReturnsProviderExistsError()
    {
        // Same type + same name → "ProviderExists" error.
        //
        // Arrange: Register "ed25519" twice
        // Act: second Register call
        // Assert: result.Error.Key == "ProviderExists"
        await Assert.Fail("stub — implementation depends on NamedProviderRegistry");
    }

    #endregion

    #region Get

    [Test]
    public async Task Get_DefaultProvider_ReturnsIsDefaultTrue()
    {
        // Get<T>() (no name) returns the IsDefault provider.
        //
        // Arrange: Register two providers, first is default
        // Act: Get<ISigningProvider>() with no name
        // Assert: returned provider is the first one with IsDefault == true
        await Assert.Fail("stub — implementation depends on NamedProviderRegistry");
    }

    [Test]
    public async Task Get_NoneRegistered_ReturnsNull()
    {
        // No providers → null.
        //
        // Arrange: fresh registry, nothing registered
        // Act: Get<ISigningProvider>()
        // Assert: result is null
        await Assert.Fail("stub — implementation depends on NamedProviderRegistry");
    }

    [Test]
    public async Task Get_ByName_NonExistent_ReturnsNull()
    {
        // Unknown name → null.
        //
        // Arrange: Register "ed25519"
        // Act: Get<ISigningProvider>("ecdsa")
        // Assert: result is null
        await Assert.Fail("stub — implementation depends on NamedProviderRegistry");
    }

    #endregion

    #region Remove

    [Test]
    public async Task Remove_NonDefaultProvider_Succeeds()
    {
        // Remove non-default → gone.
        //
        // Arrange: Register "first" (default) and "second" (non-default)
        // Act: Remove "second"
        // Assert: Get("second") returns null, Get("first") still works
        await Assert.Fail("stub — implementation depends on NamedProviderRegistry");
    }

    [Test]
    public async Task Remove_DefaultProvider_ReturnsCannotRemoveDefaultError()
    {
        // Removing default → "CannotRemoveDefault" error.
        //
        // Arrange: Register one provider (auto-default)
        // Act: Remove it
        // Assert: result.Error.Key == "CannotRemoveDefault"
        await Assert.Fail("stub — implementation depends on NamedProviderRegistry");
    }

    [Test]
    public async Task Remove_NonExistent_ReturnsProviderNotFoundError()
    {
        // Non-existent name → "ProviderNotFound" error.
        //
        // Arrange: empty registry
        // Act: Remove("unknown")
        // Assert: result.Error.Key == "ProviderNotFound"
        await Assert.Fail("stub — implementation depends on NamedProviderRegistry");
    }

    #endregion

    #region SetDefault

    [Test]
    public async Task SetDefault_ExistingProvider_BecomesDefault()
    {
        // SetDefault clears old, sets new.
        //
        // Arrange: Register "first" (default) and "second"
        // Act: SetDefault("second")
        // Assert: "second" is now default, "first" is not
        await Assert.Fail("stub — implementation depends on NamedProviderRegistry");
    }

    [Test]
    public async Task SetDefault_NonExistent_ReturnsProviderNotFoundError()
    {
        // Non-existent name → "ProviderNotFound" error.
        //
        // Arrange: Register "ed25519"
        // Act: SetDefault("unknown")
        // Assert: result.Error.Key == "ProviderNotFound"
        await Assert.Fail("stub — implementation depends on NamedProviderRegistry");
    }

    #endregion

    #region List

    [Test]
    public async Task List_ReturnsAllProvidersOfType()
    {
        // 3 providers → List returns all 3.
        //
        // Arrange: Register 3 providers with distinct names
        // Act: List<ISigningProvider>()
        // Assert: count == 3, all names present
        await Assert.Fail("stub — implementation depends on NamedProviderRegistry");
    }

    #endregion

    #region Sub-engine scope

    [Test]
    public async Task SubEngine_InheritsParentProviders()
    {
        // Sub-engine resolves parent's provider.
        //
        // Arrange: Register provider on parent engine
        // Act: Create sub-engine context, Get provider
        // Assert: sub-engine sees parent's provider
        await Assert.Fail("stub — implementation depends on NamedProviderRegistry");
    }

    [Test]
    public async Task SubEngine_LocalOverlay_DoesNotAffectParent()
    {
        // Sub-engine registers locally, parent unchanged.
        //
        // Arrange: Register "ed25519" on parent
        // Act: Sub-engine registers "mock" locally
        // Assert: parent does not see "mock", sub-engine sees both
        await Assert.Fail("stub — implementation depends on NamedProviderRegistry");
    }

    [Test]
    public async Task SubEngine_LocalOverlay_ClearedOnPoolReturn()
    {
        // On pool return, sub-engine's local overlay is cleared.
        //
        // Arrange: Register "ed25519" on parent, sub-engine registers "mock" locally
        // Act: return sub-engine to pool (clear local overlay)
        // Assert: sub-engine no longer sees "mock", still sees parent's "ed25519"
        await Assert.Fail("stub — implementation depends on NamedProviderRegistry");
    }

    [Test]
    public async Task SubEngine_FallsBackToParent_WhenLocalOverlayLacksProvider()
    {
        // Sub-engine has local providers, but falls back to parent for a different one.
        //
        // Arrange: Register "ed25519" on parent, sub-engine registers "mock" locally (different name)
        // Act: sub-engine Get<ISigningProvider>("ed25519") — not in local overlay
        // Assert: returns parent's "ed25519" provider (fallback works even with local overlay present)
        await Assert.Fail("stub — implementation depends on NamedProviderRegistry");
    }

    #endregion
}
