using PLang.Runtime2.Engine.Context;
using PLang.Runtime2.Engine.Memory;
using PLang.Runtime2.modules.identity;
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

    private IdentityVariable CreateTestIdentity() => new()
    {
        Name = "test",
        PublicKey = "dGVzdHB1YmxpY2tleQ==",
        PrivateKey = "dGVzdHByaXZhdGVrZXk=",
        IsDefault = true,
        IsArchived = false,
        Created = new DateTimeOffset(2026, 1, 15, 10, 30, 0, TimeSpan.Zero)
    };

    [Test]
    public async Task ToString_ReturnsPublicKey()
    {
        var identity = CreateTestIdentity();
        await Assert.That(identity.ToString()).IsEqualTo("dGVzdHB1YmxpY2tleQ==");
    }

    [Test]
    public async Task DotNavigation_Name_ReturnsName()
    {
        var identity = CreateTestIdentity();
        var data = new Data("identity", identity);
        var child = data.GetChild("Name");
        await Assert.That(child).IsNotNull();
        await Assert.That(child!.Value?.ToString()).IsEqualTo("test");
    }

    [Test]
    public async Task DotNavigation_PublicKey_ReturnsPublicKey()
    {
        var identity = CreateTestIdentity();
        var data = new Data("identity", identity);
        var child = data.GetChild("PublicKey");
        await Assert.That(child).IsNotNull();
        await Assert.That(child!.Value?.ToString()).IsEqualTo("dGVzdHB1YmxpY2tleQ==");
    }

    [Test]
    public async Task DotNavigation_Created_ReturnsCreated()
    {
        var identity = CreateTestIdentity();
        var data = new Data("identity", identity);
        var child = data.GetChild("Created");
        await Assert.That(child).IsNotNull();
        await Assert.That(child!.Value).IsTypeOf<DateTimeOffset>();
    }

    [Test]
    public async Task DotNavigation_IsArchived_ReturnsIsArchived()
    {
        var identity = CreateTestIdentity();
        var data = new Data("identity", identity);
        var child = data.GetChild("IsArchived");
        await Assert.That(child).IsNotNull();
        await Assert.That(child!.Value).IsEqualTo(false);
    }

    [Test]
    public async Task DotNavigation_IsDefault_ReturnsIsDefault()
    {
        var identity = CreateTestIdentity();
        var data = new Data("identity", identity);
        var child = data.GetChild("IsDefault");
        await Assert.That(child).IsNotNull();
        await Assert.That(child!.Value).IsEqualTo(true);
    }

    [Test]
    public async Task DotNavigation_PrivateKey_ReturnsPrivateKey()
    {
        // [Sensitive] is serialization only, not access control — dot navigation works
        var identity = CreateTestIdentity();
        var data = new Data("identity", identity);
        var child = data.GetChild("PrivateKey");
        await Assert.That(child).IsNotNull();
        await Assert.That(child!.Value?.ToString()).IsEqualTo("dGVzdHByaXZhdGVrZXk=");
    }
}
