using PLang.Runtime2.Engine.Context;
using PLang.Runtime2.Engine.DataSource;
using PLang.Runtime2.Engine.Errors;
using PLang.Runtime2.Engine.Memory;
using PLangEngine = PLang.Runtime2.Engine.@this;

namespace PLang.Tests.Runtime2.Modules.identity;

public class IdentityHandlerTests
{
    private string _tempDir = null!;
    private PLangEngine _engine = null!;

    [Before(Test)]
    public void Setup()
    {
        _tempDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "plang_test_identity_" + Guid.NewGuid().ToString("N")[..8]);
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

    // --- create ---

    [Test]
    public async Task Create_GeneratesValidEd25519KeyPair()
    {
        // Keys are non-null, base64-decodable, correct lengths (32 bytes public, 32 bytes private)
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task Create_DefaultFalse_IsNotDefault()
    {
        // SetAsDefault defaults to false, so identity is NOT default even if first
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task Create_SetAsDefaultTrue_BecomesDefault()
    {
        // Explicitly setting SetAsDefault=true makes it default
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task Create_SetAsDefaultTrue_ClearsPreviousDefault()
    {
        // Old default loses IsDefault when new one takes over
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task Create_StoresInSystemDataSource()
    {
        // Retrievable via System.DataSource.Get("identity", name)
        Assert.Fail("Not implemented");
    }

    // --- get ---

    [Test]
    public async Task Get_ByName_ReturnsMatchingIdentity()
    {
        // Finds identity by name
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task Get_NullName_ReturnsDefaultIdentity()
    {
        // Returns the one with IsDefault=true
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task Get_NoIdentitiesExist_AutoCreatesDefault()
    {
        // Creates "default" identity on first access when none exist
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task Get_ReturnsIdentityVariable_WithAllProperties()
    {
        // Name, PublicKey, PrivateKey, IsDefault, Created all populated
        Assert.Fail("Not implemented");
    }

    // --- getAll ---

    [Test]
    public async Task GetAll_ReturnsOnlyNonArchived()
    {
        // Archived identities excluded from result
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task GetAll_AllArchived_ReturnsEmptyList()
    {
        // Empty result, not error
        Assert.Fail("Not implemented");
    }

    // --- archive ---

    [Test]
    public async Task Archive_NonDefaultIdentity_SetsIsArchivedTrue()
    {
        // Non-default identity gets archived successfully
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task Archive_DefaultIdentity_ReturnsError()
    {
        // Cannot archive the default identity — must set different default first
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task Archive_NonExistentName_ReturnsError()
    {
        // Error path for missing identity
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task Archive_AlreadyArchived_IsIdempotent()
    {
        // Archiving already-archived identity succeeds (idempotent)
        Assert.Fail("Not implemented");
    }

    // --- setDefault ---

    [Test]
    public async Task SetDefault_SwitchesDefault_ClearsOldDefault()
    {
        // Only one default at a time — old default loses IsDefault
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task SetDefault_ArchivedOrMissing_ReturnsError()
    {
        // Can't set archived or non-existent identity as default
        Assert.Fail("Not implemented");
    }

    // --- export ---

    [Test]
    public async Task Export_ReturnsPrivateKeyString()
    {
        // Returns the raw private key string, not the object
        Assert.Fail("Not implemented");
    }
}
