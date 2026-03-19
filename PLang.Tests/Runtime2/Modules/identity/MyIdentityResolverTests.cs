using PLang.Runtime2.Engine.Context;
using PLang.Runtime2.Engine.Memory;
using PLangEngine = PLang.Runtime2.Engine.@this;

namespace PLang.Tests.Runtime2.Modules.identity;

public class MyIdentityResolverTests
{
    private string _tempDir = null!;
    private PLangEngine _engine = null!;

    [Before(Test)]
    public void Setup()
    {
        _tempDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "plang_test_myid_" + Guid.NewGuid().ToString("N")[..8]);
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
    public async Task MyIdentity_ResolvesOnFirstAccess_AutoCreates()
    {
        // memoryStack.Get("MyIdentity") returns IdentityVariable when none exist (auto-creates default)
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task MyIdentity_DotNotation_Name()
    {
        // memoryStack.Get("MyIdentity.Name") returns the identity name
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task MyIdentity_DotNotation_PublicKey()
    {
        // memoryStack.Get("MyIdentity.PublicKey") returns the public key
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task MyIdentity_StringContext_ReturnsPublicKey()
    {
        // ToString() gives public key, just like IdentityVariable.ToString()
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task MyIdentity_UpdatedAfterSetDefault()
    {
        // After switching default identity, %MyIdentity% reflects new identity
        Assert.Fail("Not implemented");
    }
}
