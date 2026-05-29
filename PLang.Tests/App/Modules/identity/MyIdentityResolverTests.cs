using app.actor.context;
using app.variable;
using app.module.identity;
using PLangEngine = global::app.@this;

namespace PLang.Tests.App.Modules.identity;

public class MyIdentityResolverTests
{
    private string _tempDir = null!;
    private PLangEngine _app = null!;

    [Before(Test)]
    public void Setup()
    {
        _tempDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "plang_test_myid_" + Guid.NewGuid().ToString("N")[..8]);
        System.IO.Directory.CreateDirectory(_tempDir);
        _app = new PLangEngine(_tempDir);
    }

    [After(Test)]
    public void Cleanup()
    {
        try
        {
            _app.DisposeAsync().AsTask().GetAwaiter().GetResult();
            if (System.IO.Directory.Exists(_tempDir))
                System.IO.Directory.Delete(_tempDir, true);
        }
        catch { /* best effort cleanup */ }
    }

    [Test]
    public async Task MyIdentity_ResolvesOnFirstAccess_AutoCreates()
    {
        // Access %MyIdentity% via Variables — should auto-create default identity
        var data = _app.System.Context.Variables.Get("MyIdentity");
        await Assert.That(data).IsNotNull();

        var identity = data!.Value as Identity;
        await Assert.That(identity).IsNotNull();
        await Assert.That(identity!.Name).IsEqualTo("default");
        await Assert.That(identity.IsDefault).IsTrue();
    }

    [Test]
    public async Task MyIdentity_DotNotation_Name()
    {
        // DynamicData auto-creates on access
        var data = _app.System.Context.Variables.Get("MyIdentity");
        await Assert.That(data).IsNotNull();

        var identity = data!.Value as Identity;
        await Assert.That(identity).IsNotNull();
        await Assert.That(identity!.Name).IsEqualTo("default");
    }

    [Test]
    public async Task MyIdentity_DotNotation_PublicKey()
    {
        var data = _app.System.Context.Variables.Get("MyIdentity");
        var child = data!.GetChild("PublicKey");
        await Assert.That(child).IsNotNull();
        await Assert.That(child!.Value?.ToString()).IsNotNull();

        // Should be base64
        var bytes = Convert.FromBase64String(child.Value!.ToString()!);
        await Assert.That(bytes.Length).IsEqualTo(32);
    }

    [Test]
    public async Task MyIdentity_StringContext_ReturnsPublicKey()
    {
        var data = _app.System.Context.Variables.Get("MyIdentity");
        var identity = data!.Value as Identity;

        // ToString() should return the public key
        await Assert.That(identity!.ToString()).IsEqualTo(identity.PublicKey);
    }

    [Test]
    public async Task MyIdentity_UpdatedAfterSetDefault()
    {
        var ctx = _app.System.Context;

        // Create two identities
        var h1 = new Create { Context = ctx, Name = "first", SetAsDefault = true };
        await h1.Run();
        var h2 = new Create { Context = ctx, Name = "second", SetAsDefault = false };
        await h2.Run();

        // Verify %MyIdentity% is "first" — DynamicData re-evaluates on each access
        var data1 = _app.System.Context.Variables.Get("MyIdentity");
        var id1 = data1!.Value as Identity;
        await Assert.That(id1!.Name).IsEqualTo("first");

        // Switch default
        var setDefault = new SetDefault { Context = ctx, Name = "second" };
        await setDefault.Run();

        // %MyIdentity% should now be "second" — DynamicData lambda calls provider again
        var data2 = _app.System.Context.Variables.Get("MyIdentity");
        var id2 = data2!.Value as Identity;
        await Assert.That(id2!.Name).IsEqualTo("second");
    }
}
