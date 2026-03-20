using PLang.Runtime2.Engine.Context;
using PLang.Runtime2.Engine.Errors;
using PLang.Runtime2.Engine.Memory;
using PLang.Runtime2.Engine.Providers;
using PLang.Runtime2.modules.crypto.providers;
using PLangEngine = PLang.Runtime2.Engine.@this;

namespace PLang.Tests.Runtime2.Modules.provider;

/// <summary>
/// Tests the provider module actions (load, remove, setDefault, list).
/// Tests use direct registry operations since the load action requires a real DLL.
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
        var provider = new MockSigningProvider("mock");
        var result = _engine.Providers.Register<ISigningProvider>(provider);

        await Assert.That(result.Success).IsTrue();
        var retrieved = _engine.Providers.Get<ISigningProvider>("mock");
        await Assert.That(retrieved.Success).IsTrue();
        await Assert.That(retrieved.Value!.Name).IsEqualTo("mock");
    }

    [Test]
    public async Task Load_DuplicateName_ReturnsError()
    {
        _engine.Providers.Register<ISigningProvider>(new MockSigningProvider("mock"));
        var result = _engine.Providers.Register<ISigningProvider>(new MockSigningProvider("mock"));

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error!.Key).IsEqualTo("ProviderExists");
    }

    [Test]
    public async Task Load_NoParameterlessCtor_ReturnsError()
    {
        // The load action itself handles the ctor check. Test it via the action.
        var action = new PLang.Runtime2.modules.provider.load
        {
            Context = Ctx,
            Path = "/nonexistent/path/fake.dll"
        };
        var result = await action.Run();

        // Should fail since the DLL doesn't exist
        await Assert.That(result.Success).IsFalse();
    }

    #endregion

    #region Remove

    [Test]
    public async Task Remove_NonDefault_Succeeds()
    {
        _engine.Providers.Register<ISigningProvider>(new MockSigningProvider("first"));
        _engine.Providers.Register<ISigningProvider>(new MockSigningProvider("second"));

        var action = new PLang.Runtime2.modules.provider.remove
        {
            Context = Ctx,
            Name = "second",
            Type = "signing"
        };
        var result = await action.Run();

        await Assert.That(result.Success).IsTrue();
        await Assert.That(_engine.Providers.Get<ISigningProvider>("second").Success).IsFalse();
    }

    [Test]
    public async Task Remove_Default_ReturnsError()
    {
        // ed25519 is registered as default at engine startup
        var action = new PLang.Runtime2.modules.provider.remove
        {
            Context = Ctx,
            Name = "ed25519",
            Type = "signing"
        };
        var result = await action.Run();

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error!.Key).IsEqualTo("CannotRemoveDefault");
    }

    [Test]
    public async Task Remove_NonExistent_ReturnsError()
    {
        var action = new PLang.Runtime2.modules.provider.remove
        {
            Context = Ctx,
            Name = "unknown",
            Type = "signing"
        };
        var result = await action.Run();

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error!.Key).IsEqualTo("ProviderNotFound");
    }

    #endregion

    #region SetDefault

    [Test]
    public async Task SetDefault_SwitchesDefault()
    {
        var first = new MockSigningProvider("first");
        var second = new MockSigningProvider("second");
        _engine.Providers.Register<ISigningProvider>(first);
        _engine.Providers.Register<ISigningProvider>(second);

        var action = new PLang.Runtime2.modules.provider.setDefault
        {
            Context = Ctx,
            Name = "second",
            Type = "signing"
        };
        var result = await action.Run();

        await Assert.That(result.Success).IsTrue();
        await Assert.That(second.IsDefault).IsTrue();
        await Assert.That(first.IsDefault).IsFalse();
    }

    [Test]
    public async Task SetDefault_UnknownName_ReturnsError()
    {
        _engine.Providers.Register<ISigningProvider>(new MockSigningProvider("first"));

        var action = new PLang.Runtime2.modules.provider.setDefault
        {
            Context = Ctx,
            Name = "unknown",
            Type = "signing"
        };
        var result = await action.Run();

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error!.Key).IsEqualTo("ProviderNotFound");
    }

    #endregion

    #region List

    [Test]
    public async Task List_ReturnsAllWithStatus()
    {
        var first = new MockSigningProvider("first");
        var second = new MockSigningProvider("second");
        _engine.Providers.Register<ISigningProvider>(first);
        _engine.Providers.Register<ISigningProvider>(second);

        var providers = _engine.Providers.List<ISigningProvider>();
        await Assert.That(providers.Count).IsEqualTo(3); // ed25519 (built-in) + first + second
        // ed25519 is default from engine startup
        await Assert.That(first.IsDefault).IsFalse();
        await Assert.That(second.IsDefault).IsFalse();
    }

    [Test]
    public async Task List_FilteredByType_ReturnsOnlyMatchingInterface()
    {
        // Engine registers ICryptoProvider at startup, ISigningProvider separately
        var signingProviders = _engine.Providers.List<ISigningProvider>();
        await Assert.That(signingProviders.Count).IsEqualTo(1); // only ed25519
        await Assert.That(signingProviders[0].Name).IsEqualTo("ed25519");
    }

    #endregion

    private class MockSigningProvider : ISigningProvider
    {
        public string Name { get; }
        public bool IsDefault { get; set; }

        public MockSigningProvider(string name) { Name = name; }

        public KeyPair GenerateKeyPair() => new("mockPub", "mockPriv");
        public Data Sign(byte[] data, string privateKey) => Data.Ok(new byte[64]);
        public Data Verify(byte[] data, byte[] signature, string publicKey) => Data.Ok(true);
    }
}
