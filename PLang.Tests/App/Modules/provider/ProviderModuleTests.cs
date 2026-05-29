using app.actor.context;
using app.error;
using app.variable;
using app.modules.code;
using app.modules.signing;
using app.modules.signing.code;
using app.modules.crypto.code;
using PLangEngine = global::app.@this;

namespace PLang.Tests.App.Modules.provider;

/// <summary>
/// Tests the provider module actions (load, remove, setDefault, list).
/// Tests use direct registry operations since the load action requires a real DLL.
/// </summary>
public class ProviderModuleTests
{
    private string _tempDir = null!;
    private PLangEngine _app = null!;

    [Before(Test)]
    public void Setup()
    {
        _tempDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "plang_test_provider_" + Guid.NewGuid().ToString("N")[..8]);
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

    private global::app.actor.context.@this Ctx => _app.System.Context;

    // Fixture DLL paths — pre-built DLLs committed under PLang.Tests/App/Fixtures/dlls/
    private static readonly string FixtureBase = System.IO.Path.GetFullPath(
        System.IO.Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "App", "Fixtures", "dlls"));

    private static string FixtureDll(string project) =>
        System.IO.Path.Combine(FixtureBase, $"{project}.dll");

    /// <summary>
    /// Pre-grant Execute (+ Read) on a fixture DLL path. The fixture DLLs
    /// live outside the per-test App root, so AuthGate would prompt
    /// without an explicit grant.
    /// </summary>
    private async Task GrantExecute(string dllPath)
    {
        // Use the exact resolved path so the grant's Path key matches the
        // canonical-form path that AuthGate compares against.
        var resolved = global::app.types.path.@this.Resolve("/" + dllPath, Ctx);
        var verb = new global::app.types.path.permission.verb.@this
        {
            Read = new global::app.types.path.permission.verb.Read(),
            Execute = new global::app.types.path.permission.verb.Execute()
        };
        var permission = new global::app.types.path.permission.@this(
            Actor: _app.System.Name,
            Path: resolved.Absolute,
            Verb: verb,
            Match: global::app.types.path.permission.Match.Exact);
        var data = new global::app.data.@this<global::app.types.path.permission.@this>("", permission) { Context = Ctx };
        await _app.System.Permission.Add(data);
    }

    #region Load

    [Test]
    public async Task Load_RegistersProviderByName()
    {
        var provider = new MockSigningProvider("mock");
        var result = _app.Code.Register<ISigning>(provider);

        await Assert.That(result.Success).IsTrue();
        var retrieved = _app.Code.Get<ISigning>("mock");
        await Assert.That(retrieved.Success).IsTrue();
        await Assert.That(retrieved.Value!.Name).IsEqualTo("mock");
    }

    [Test]
    public async Task Load_DuplicateName_ReturnsError()
    {
        _app.Code.Register<ISigning>(new MockSigningProvider("mock"));
        var result = _app.Code.Register<ISigning>(new MockSigningProvider("mock"));

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error!.Key).IsEqualTo("ProviderExists");
    }

    [Test]
    public async Task Load_NonExistentDll_ReturnsLoadError()
    {
        var action = new global::app.modules.code.load
        {
            Context = Ctx,
            Path = global::app.data.@this<global::app.types.path.@this>.Ok(global::app.types.path.@this.Resolve("/nonexistent/path/fake.dll", Ctx))
        };
        var result = await action.Run();

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error!.Key).IsEqualTo("LoadError");
    }

    [Test]
    public async Task Load_NullPath_ReturnsValidationError()
    {
        var action = new global::app.modules.code.load
        {
            Context = Ctx,
            Path = null
        };
        var result = await action.Run();

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error!.Key).IsEqualTo("ValidationError");
    }

    [Test]
    public async Task LoadAction_ValidDll_RegistersProvider()
    {
        var dllPath = FixtureDll("TestProvider");
        await GrantExecute(dllPath);
        if (!System.IO.File.Exists(dllPath))
        {
            Assert.Fail($"Fixture DLL not found: {dllPath}. Build TestProvider project first.");
            return;
        }

        var action = new global::app.modules.code.load
        {
            Context = Ctx,
            Path = global::app.data.@this<global::app.types.path.@this>.Ok(global::app.types.path.@this.Resolve("/" + dllPath, Ctx)),
        };
        var result = await action.Run();

        await Assert.That(result.Success).IsTrue();
        var loaded = _app.Code.Get<ISigning>("test-signing");
        await Assert.That(loaded.Success).IsTrue();
        await Assert.That(loaded.Value!.Name).IsEqualTo("test-signing");
    }

    [Test]
    public async Task LoadAction_EmptyDll_ReturnsNoProviders()
    {
        var dllPath = FixtureDll("EmptyProvider");
        await GrantExecute(dllPath);
        if (!System.IO.File.Exists(dllPath))
        {
            Assert.Fail($"Fixture DLL not found: {dllPath}. Build EmptyProvider project first.");
            return;
        }

        var action = new global::app.modules.code.load
        {
            Context = Ctx,
            Path = global::app.data.@this<global::app.types.path.@this>.Ok(global::app.types.path.@this.Resolve("/" + dllPath, Ctx)),
        };
        var result = await action.Run();

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error!.Key).IsEqualTo("NoProviders");
    }

    [Test]
    public async Task LoadAction_NoCtorDll_ReturnsProviderConstructorError()
    {
        var dllPath = FixtureDll("NoCtorProvider");
        await GrantExecute(dllPath);
        if (!System.IO.File.Exists(dllPath))
        {
            Assert.Fail($"Fixture DLL not found: {dllPath}. Build NoCtorProvider project first.");
            return;
        }

        var action = new global::app.modules.code.load
        {
            Context = Ctx,
            Path = global::app.data.@this<global::app.types.path.@this>.Ok(global::app.types.path.@this.Resolve("/" + dllPath, Ctx)),
        };
        var result = await action.Run();

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error!.Key).IsEqualTo("ProviderConstructor");
    }

    #endregion

    #region Remove

    [Test]
    public async Task Remove_NonDefault_Succeeds()
    {
        _app.Code.Register<ISigning>(new MockSigningProvider("first"));
        _app.Code.Register<ISigning>(new MockSigningProvider("second"));

        var action = new global::app.modules.code.remove
        {
            Context = Ctx,
            Name = "second",
            Type = "signing"
        };
        var result = await action.Run();

        await Assert.That(result.Success).IsTrue();
        await Assert.That(_app.Code.Get<ISigning>("second").Success).IsFalse();
    }

    [Test]
    public async Task Remove_Default_ReturnsError()
    {
        // ed25519 is registered as default at engine startup
        var action = new global::app.modules.code.remove
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
        var action = new global::app.modules.code.remove
        {
            Context = Ctx,
            Name = "unknown",
            Type = "signing"
        };
        var result = await action.Run();

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error!.Key).IsEqualTo("ProviderNotFound");
    }

    [Test]
    public async Task Remove_UnknownType_ReturnsError()
    {
        var action = new global::app.modules.code.remove
        {
            Context = Ctx,
            Name = "anything",
            Type = "invalid"
        };
        var result = await action.Run();

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error!.Key).IsEqualTo("UnknownType");
    }

    #endregion

    #region SetDefault

    [Test]
    public async Task SetDefault_SwitchesDefault()
    {
        var first = new MockSigningProvider("first");
        var second = new MockSigningProvider("second");
        _app.Code.Register<ISigning>(first);
        _app.Code.Register<ISigning>(second);

        var action = new global::app.modules.code.setDefault
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
        _app.Code.Register<ISigning>(new MockSigningProvider("first"));

        var action = new global::app.modules.code.setDefault
        {
            Context = Ctx,
            Name = "unknown",
            Type = "signing"
        };
        var result = await action.Run();

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error!.Key).IsEqualTo("ProviderNotFound");
    }

    [Test]
    public async Task SetDefault_UnknownType_ReturnsError()
    {
        var action = new global::app.modules.code.setDefault
        {
            Context = Ctx,
            Name = "anything",
            Type = "invalid"
        };
        var result = await action.Run();

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error!.Key).IsEqualTo("UnknownType");
    }

    #endregion

    #region List

    [Test]
    public async Task List_ReturnsAllWithStatus()
    {
        var first = new MockSigningProvider("first");
        var second = new MockSigningProvider("second");
        _app.Code.Register<ISigning>(first);
        _app.Code.Register<ISigning>(second);

        var providers = _app.Code.List<ISigning>();
        await Assert.That(providers.Count).IsEqualTo(3); // ed25519 (built-in) + first + second
        // ed25519 is default from engine startup
        await Assert.That(first.IsDefault).IsFalse();
        await Assert.That(second.IsDefault).IsFalse();
    }

    [Test]
    public async Task List_FilteredByType_ReturnsOnlyMatchingInterface()
    {
        // Engine registers ICrypto at startup, ISigning separately
        var signingProviders = _app.Code.List<ISigning>();
        await Assert.That(signingProviders.Count).IsEqualTo(1); // only ed25519
        await Assert.That(signingProviders[0].Name).IsEqualTo("ed25519");
    }

    [Test]
    public async Task ListAction_NoType_ReturnsAll()
    {
        _app.Code.Register<ISigning>(new MockSigningProvider("extra"));

        var action = new global::app.modules.code.list
        {
            Context = Ctx,
            Type = null
        };
        var result = await action.Run();

        await Assert.That(result.Success).IsTrue();
        // Returns all providers across all types
        var providers = (IReadOnlyList<ICode>)result.Value!;
        await Assert.That(providers.Count).IsGreaterThanOrEqualTo(2);
    }

    [Test]
    public async Task ListAction_ByType_ReturnsFiltered()
    {
        _app.Code.Register<ISigning>(new MockSigningProvider("extra"));

        var action = new global::app.modules.code.list
        {
            Context = Ctx,
            Type = "signing"
        };
        var result = await action.Run();

        await Assert.That(result.Success).IsTrue();
    }

    [Test]
    public async Task ListAction_UnknownType_ReturnsError()
    {
        var action = new global::app.modules.code.list
        {
            Context = Ctx,
            Type = "quantum"
        };
        var result = await action.Run();

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error!.Key).IsEqualTo("UnknownType");
    }

    #endregion

    private class MockSigningProvider : ISigning
    {
        public string Name { get; }
        public bool IsDefault { get; set; }

        public bool IsBuiltIn { get; set; }

        public string? Source { get; set; }

        public MockSigningProvider(string name) { Name = name; }

        public global::app.data.@this<KeyPair> GenerateKeyPair() => global::app.data.@this<KeyPair>.Ok(new KeyPair("mockPub", "mockPriv"));
        public global::app.data.@this<byte[]> Sign(byte[] data, string privateKey) => global::app.data.@this<byte[]>.Ok(new byte[64]);
        public global::app.data.@this<bool> Verify(byte[] data, byte[] signature, string publicKey) => global::app.data.@this<bool>.Ok(true);
        public Task<global::app.data.@this<object>> SignAsync(sign action) => Task.FromResult(new global::app.data.@this<object>());
        public Task<global::app.data.@this<bool>> VerifyAsync(verify action) => Task.FromResult(global::app.data.@this<bool>.Ok(true));
    }
}
