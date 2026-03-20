using PLang.Runtime2.Engine.Context;
using PLang.Runtime2.Engine.Errors;
using PLang.Runtime2.Engine.Memory;
using PLangEngine = PLang.Runtime2.Engine.@this;

namespace PLang.Tests.Runtime2.Modules.provider;

/// <summary>
/// Tests the provider module actions (load, remove, setDefault, list).
/// These are the PLang-facing actions for managing providers at runtime.
/// </summary>
public class ProviderModuleTests
{
    private string _tempDir = null!;
    private PLangEngine _engine = null!;

    [Before(Test)]
    public void Setup()
    {
        _tempDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "plang_test_provider_" + Guid.NewGuid().ToString("N")[..8]);
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

    #region Load

    [Test]
    public async Task Load_RegistersProviderByName()
    {
        // Load a provider → registered and retrievable by name.
        //
        // Arrange: mock provider DLL path or use MockSigningProvider
        // Act: load action with name "mock"
        // Assert: provider retrievable from registry by name
        await Assert.Fail("stub — implementation depends on provider module");
    }

    [Test]
    public async Task Load_DuplicateName_ReturnsError()
    {
        // Same name twice → "ProviderExists".
        //
        // Arrange: load "mock" provider
        // Act: load "mock" again
        // Assert: result.Error.Key == "ProviderExists"
        await Assert.Fail("stub — implementation depends on provider module");
    }

    [Test]
    public async Task Load_NoParameterlessCtor_ReturnsError()
    {
        // Provider without parameterless constructor → "ProviderConstructor" error.
        //
        // Arrange: provider type with no parameterless ctor
        // Act: load
        // Assert: result.Error.Key == "ProviderConstructor"
        await Assert.Fail("stub — implementation depends on provider module");
    }

    #endregion

    #region Remove

    [Test]
    public async Task Remove_NonDefault_Succeeds()
    {
        // Remove non-default → gone.
        //
        // Arrange: load two providers, second is non-default
        // Act: remove second
        // Assert: second no longer retrievable
        await Assert.Fail("stub — implementation depends on provider module");
    }

    [Test]
    public async Task Remove_Default_ReturnsError()
    {
        // Remove default → "CannotRemoveDefault".
        //
        // Arrange: load one provider (auto-default)
        // Act: remove it
        // Assert: result.Error.Key == "CannotRemoveDefault"
        await Assert.Fail("stub — implementation depends on provider module");
    }

    [Test]
    public async Task Remove_NonExistent_ReturnsError()
    {
        // Non-existent → "ProviderNotFound".
        //
        // Arrange: empty registry
        // Act: remove "unknown"
        // Assert: result.Error.Key == "ProviderNotFound"
        await Assert.Fail("stub — implementation depends on provider module");
    }

    #endregion

    #region SetDefault

    [Test]
    public async Task SetDefault_SwitchesDefault()
    {
        // Old cleared, new set.
        //
        // Arrange: load "first" (default) and "second"
        // Act: setDefault "second"
        // Assert: "second" is default, "first" is not
        await Assert.Fail("stub — implementation depends on provider module");
    }

    #endregion

    #region List

    [Test]
    public async Task List_ReturnsAllWithStatus()
    {
        // 2 providers → both listed with correct IsDefault status.
        //
        // Arrange: load "first" and "second"
        // Act: list
        // Assert: 2 entries, first has IsDefault=true, second has IsDefault=false
        await Assert.Fail("stub — implementation depends on provider module");
    }

    #endregion
}
