using PLang.Runtime2.Engine.Context;
using PLang.Runtime2.Engine.Memory;
using PLangEngine = PLang.Runtime2.Engine.@this;

namespace PLang.Tests.Runtime2.Modules.identity;

public class IdentityVariableTests
{
    private string _tempDir = null!;
    private PLangEngine _engine = null!;

    [Before(Test)]
    public void Setup()
    {
        _tempDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "plang_test_idvar_" + Guid.NewGuid().ToString("N")[..8]);
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

    [Test]
    public async Task ToString_ReturnsPublicKey()
    {
        // String context gives public key (not private key, not name)
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task DotNavigation_Name_ReturnsName()
    {
        // GetChild("Name") returns the identity name
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task DotNavigation_PublicKey_ReturnsPublicKey()
    {
        // GetChild("PublicKey") returns the public key
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task DotNavigation_Created_ReturnsCreated()
    {
        // GetChild("Created") returns the creation timestamp
        Assert.Fail("Not implemented");
    }
}
