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
        _app = TestApp.Plain(_tempDir);
        global::PLang.Tests.TestApp.UseSharedIdentity(_app);
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
        var data = await _app.System.Context.Variable.Get("MyIdentity");
        await Assert.That(data).IsNotNull();

        var identity = (await data!.Value()) as Identity;
        await Assert.That(identity).IsNotNull();
        await Assert.That(identity!.Name).IsEqualTo("default");
        await Assert.That(identity.IsDefault).IsTrue();
    }

    [Test]
    public async Task MyIdentity_DotNotation_Name()
    {
        // DynamicData auto-creates on access
        var data = await _app.System.Context.Variable.Get("MyIdentity");
        await Assert.That(data).IsNotNull();

        var identity = (await data!.Value()) as Identity;
        await Assert.That(identity).IsNotNull();
        await Assert.That(identity!.Name).IsEqualTo("default");
    }

    [Test]
    public async Task MyIdentity_DotNotation_PublicKey()
    {
        var data = await _app.System.Context.Variable.Get("MyIdentity");
        var child = await data!.Get("PublicKey");
        await Assert.That(child).IsNotNull();
        await Assert.That((await child!.Value())?.ToString()).IsNotNull();

        // Should be base64
        var bytes = Convert.FromBase64String((await child.Value())!.ToString()!);
        await Assert.That(bytes.Length).IsEqualTo(32);
    }

    [Test]
    public async Task MyIdentity_StringContext_ReturnsPublicKey()
    {
        var data = await _app.System.Context.Variable.Get("MyIdentity");
        var identity = (await data!.Value()) as Identity;

        // ToString() should return the public key
        await Assert.That(identity!.ToString()).IsEqualTo(identity.PublicKey);
    }

    [Test]
    public async Task MyIdentity_UpdatedAfterSetDefault()
    {
        var context = _app.System.Context;

        // Create two identities
        var h1 = new Create(context) { Name = (global::app.type.text.@this)"first", SetAsDefault = (global::app.type.@bool.@this)true };
        await h1.Attach(null, context);
        await h1.Run();
        var h2 = new Create(context) { Name = (global::app.type.text.@this)"second", SetAsDefault = (global::app.type.@bool.@this)false };
        await h2.Attach(null, context);
        await h2.Run();

        // Verify %MyIdentity% is "first" — DynamicData re-evaluates on each access
        var data1 = await _app.System.Context.Variable.Get("MyIdentity");
        var id1 = (await data1!.Value()) as Identity;
        await Assert.That(id1!.Name).IsEqualTo("first");

        // Switch default
        var setDefault = new SetDefault(context) { Name = (global::app.type.text.@this)"second" };
        await setDefault.Attach(null, context);
        await setDefault.Run();

        // %MyIdentity% should now be "second" — DynamicData lambda calls provider again
        var data2 = await _app.System.Context.Variable.Get("MyIdentity");
        var id2 = (await data2!.Value()) as Identity;
        await Assert.That(id2!.Name).IsEqualTo("second");
    }
}
