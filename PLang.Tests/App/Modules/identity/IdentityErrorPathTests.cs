using System.Reflection;
using app.actor.context;
using app.modules.settings;
using app.error;
using app.variables;
using app.modules.identity;
using PLangEngine = global::app.@this;

namespace PLang.Tests.App.Modules.identity;

/// <summary>
/// Tests for identity module error paths:
/// - GetOrCreateDefaultAsync promote/auto-create save failures (via Get action)
/// - Handler catch blocks (export.cs, get.cs, Identity.cs)
/// - Handler save/remove failures (create, setDefault, rename, archive, unarchive)
/// - LoadAllAsync when DataSource.GetAll fails (via GetAll action)
/// - Deserialize with unrecognized value types (via Get action)
/// </summary>
public class IdentityErrorPathTests
{
    private string _tempDir = null!;
    private PLangEngine _app = null!;

    [Before(Test)]
    public void Setup()
    {
        _tempDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "plang_test_iderr_" + Guid.NewGuid().ToString("N")[..8]);
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

    // --- GetOrCreateDefaultAsync: auto-create save failure (via Get action) ---

    [Test]
    public async Task GetOrCreateDefault_AutoCreateSaveFails_ReturnsError()
    {
        // No identities exist → auto-create path → save fails
        SwapDataSource(_app, new FailingSaveDataSource(
            _app.SettingsStore));

        var result = await new global::app.modules.identity.Get { Context = Ctx, Name = null }.Run();
        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error!.Key).IsEqualTo("IOError");
    }

    [Test]
    public async Task GetOrCreateDefault_PromoteSaveFails_ReturnsError()
    {
        // Create a non-default, non-archived identity first (using real DataSource)
        var create = new Create { Context = Ctx, Name = "candidate", SetAsDefault = false };
        var createResult = await create.Run();
        await Assert.That(createResult.Success).IsTrue();

        // Now swap to failing DataSource — GetAll still works (delegates), but Set fails
        SwapDataSource(_app, new FailingSaveDataSource(
            _app.SettingsStore));

        var result = await new global::app.modules.identity.Get { Context = Ctx, Name = null }.Run();
        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error!.Key).IsEqualTo("IOError");
    }

    // --- Handler catch blocks for save failures ---

    [Test]
    public async Task Get_NullName_SaveFails_ReturnsError()
    {
        // Swap to failing save — Get(null) calls GetOrCreateDefaultAsync which returns error
        SwapDataSource(_app, new FailingSaveDataSource(
            _app.SettingsStore));

        var handler = new global::app.modules.identity.Get { Context = Ctx, Name = null };
        var result = await handler.Run();

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error).IsNotNull();
        await Assert.That(result.Error!.Key).IsEqualTo("IOError");
    }

    [Test]
    public async Task Export_NullName_SaveFails_ReturnsError()
    {
        // Swap to failing save — Export(null) calls GetOrCreateDefaultAsync which returns error
        SwapDataSource(_app, new FailingSaveDataSource(
            _app.SettingsStore));

        var handler = new Export { Context = Ctx, Name = null };
        var result = await handler.Run();

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error).IsNotNull();
        await Assert.That(result.Error!.Key).IsEqualTo("IOError");
    }

    [Test]
    public async Task MyIdentity_ResolveDefault_SaveFails_ReturnsNull()
    {
        // Swap to failing save before %MyIdentity% resolves
        SwapDataSource(_app, new FailingSaveDataSource(
            _app.SettingsStore));

        // Access %MyIdentity% — DynamicData lambda calls provider, which fails, returns null
        var data = _app.User.Context.Variables.Get("MyIdentity");
        await Assert.That(data).IsNotNull();
        await Assert.That(data!.Value).IsNull();
    }

    // --- Handler save/remove failure paths ---

    [Test]
    public async Task Create_ClearDefaultSaveFails_ReturnsError()
    {
        // Create an existing default identity
        var h = new Create { Context = Ctx, Name = "existing", SetAsDefault = true };
        await h.Run();

        // Swap to failing save — clearing old default fails
        SwapDataSource(_app, new FailingSaveDataSource(
            _app.SettingsStore));

        var handler = new Create { Context = Ctx, Name = "new", SetAsDefault = true };
        var result = await handler.Run();

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error).IsNotNull();
        await Assert.That(result.Error!.Key).IsEqualTo("IOError");
    }

    [Test]
    public async Task Create_SaveNewIdentityFails_ReturnsError()
    {
        // Swap to failing save — saving the new identity fails
        SwapDataSource(_app, new FailingSaveDataSource(
            _app.SettingsStore));

        var handler = new Create { Context = Ctx, Name = "newid", SetAsDefault = false };
        var result = await handler.Run();

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error).IsNotNull();
        await Assert.That(result.Error!.Key).IsEqualTo("IOError");
    }

    [Test]
    public async Task SetDefault_ClearOldDefaultSaveFails_ReturnsError()
    {
        // Create two identities: one default, one not
        var h1 = new Create { Context = Ctx, Name = "old", SetAsDefault = true };
        await h1.Run();
        var h2 = new Create { Context = Ctx, Name = "new", SetAsDefault = false };
        await h2.Run();

        // Swap to failing save — clearing old default fails
        SwapDataSource(_app, new FailingSaveDataSource(
            _app.SettingsStore));

        var handler = new SetDefault { Context = Ctx, Name = "new" };
        var result = await handler.Run();

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error).IsNotNull();
        await Assert.That(result.Error!.Key).IsEqualTo("IOError");
    }

    [Test]
    public async Task SetDefault_SaveNewDefaultFails_ReturnsError()
    {
        // Create a single non-default identity (no existing defaults to clear)
        var h = new Create { Context = Ctx, Name = "target", SetAsDefault = false };
        await h.Run();

        // Swap to failing save — saving the new default fails
        SwapDataSource(_app, new FailingSaveDataSource(
            _app.SettingsStore));

        var handler = new SetDefault { Context = Ctx, Name = "target" };
        var result = await handler.Run();

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error).IsNotNull();
        await Assert.That(result.Error!.Key).IsEqualTo("IOError");
    }

    [Test]
    public async Task Rename_SaveNewNameFails_ReturnsError()
    {
        var h = new Create { Context = Ctx, Name = "oldname", SetAsDefault = false };
        await h.Run();

        // Swap to failing save — saving with new name fails
        SwapDataSource(_app, new FailingSaveDataSource(
            _app.SettingsStore));

        var handler = new Rename { Context = Ctx, Name = "oldname", NewName = "newname" };
        var result = await handler.Run();

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error).IsNotNull();
        await Assert.That(result.Error!.Key).IsEqualTo("IOError");
    }

    [Test]
    public async Task Rename_RemoveOldNameFails_ReturnsError()
    {
        var h = new Create { Context = Ctx, Name = "oldname", SetAsDefault = false };
        await h.Run();

        // Swap to failing remove — save succeeds but remove fails
        SwapDataSource(_app, new FailingRemoveDataSource(
            _app.SettingsStore));

        var handler = new Rename { Context = Ctx, Name = "oldname", NewName = "newname" };
        var result = await handler.Run();

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error).IsNotNull();
        await Assert.That(result.Error!.Key).IsEqualTo("IOError");
    }

    [Test]
    public async Task Archive_SaveFails_ReturnsError()
    {
        var h = new Create { Context = Ctx, Name = "toarchive", SetAsDefault = false };
        await h.Run();

        // Swap to failing save
        SwapDataSource(_app, new FailingSaveDataSource(
            _app.SettingsStore));

        var handler = new Archive { Context = Ctx, Name = "toarchive" };
        var result = await handler.Run();

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error).IsNotNull();
        await Assert.That(result.Error!.Key).IsEqualTo("IOError");
    }

    [Test]
    public async Task Unarchive_SaveFails_ReturnsError()
    {
        // Create and archive an identity
        var h = new Create { Context = Ctx, Name = "tounarchive", SetAsDefault = false };
        await h.Run();
        var archiveH = new Archive { Context = Ctx, Name = "tounarchive" };
        await archiveH.Run();

        // Swap to failing save
        SwapDataSource(_app, new FailingSaveDataSource(
            _app.SettingsStore));

        var handler = new Unarchive { Context = Ctx, Name = "tounarchive" };
        var result = await handler.Run();

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error).IsNotNull();
        await Assert.That(result.Error!.Key).IsEqualTo("IOError");
    }

    // --- LoadAllAsync when DataSource.GetAll fails (via GetAll action) ---

    [Test]
    public async Task GetAll_DataSourceFails_ReturnsError()
    {
        SwapDataSource(_app, new FailingGetAllDataSource());

        var handler = new list { Context = Ctx };
        var result = await handler.Run();
        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error).IsNotNull();
    }

    // --- Deserialize with unrecognized value type (via Get action) ---

    [Test]
    public async Task Get_UnrecognizedValueType_ReturnsEmptyIdentity()
    {
        // Store a raw integer in the identity table — deserializes as Identity with empty fields
        var ds = _app.SettingsStore;
        await ds.Set("identity", "weird", new Data("weird", 42));

        var result = await new global::app.modules.identity.Get { Context = Ctx, Name = "weird" }.Run();
        // Identity deserializes but has empty PublicKey — valid but useless
        await Assert.That(result.Success).IsTrue();
        var identity = result.Value as Identity;
        await Assert.That(identity!.PublicKey).IsEqualTo("");
    }

    [Test]
    public async Task GetAll_MixedValues_IncludesAll()
    {
        var ds = _app.SettingsStore;

        // Store a valid identity via Create action
        var create = new Create { Context = Ctx, Name = "valid", SetAsDefault = true };
        await create.Run();

        // Store a non-identity value directly — deserializes as Identity with empty fields
        await ds.Set("identity", "garbage", new Data("garbage", "just a string"));

        var handler = new list { Context = Ctx };
        var result = await handler.Run();
        await Assert.That(result.Success).IsTrue();

        var list = result.Value as List<Identity>;
        // Both deserialize as Identity — no type filtering anymore
        await Assert.That(list!.Count).IsEqualTo(2);
    }

    // --- Helpers ---

    /// <summary>
    /// Swaps the SettingsStore on App via reflection on the auto-property
    /// backing field. After stage 13's settings rework the store moved from
    /// per-actor to app-level — there's a single shared <c>app.SettingsStore</c>.
    /// </summary>
    private static void SwapDataSource(global::app.@this app, global::app.modules.settings.IStore newDataSource)
    {
        var field = typeof(global::app.@this).GetField("_settingsStore",
            BindingFlags.NonPublic | BindingFlags.Instance);
        field!.SetValue(app, new Lazy<global::app.modules.settings.IStore>(() => newDataSource));
    }

    /// <summary>
    /// DataSource wrapper that delegates all operations except Set, which always fails.
    /// </summary>
    private class FailingSaveDataSource : global::app.modules.settings.IStore
    {
        private readonly global::app.modules.settings.IStore _inner;
        public FailingSaveDataSource(global::app.modules.settings.IStore inner) => _inner = inner;

        public Task<Data> Get(string table, string key) => _inner.Get(table, key);
        public Task<Data> Get<T>(string table, string key) where T : Data => _inner.Get<T>(table, key);
        public Task<Data> GetAll(string table) => _inner.GetAll(table);
        public Task<global::app.data.@this<List<T>>> GetAll<T>(string table) where T : Data => _inner.GetAll<T>(table);
        public Task<Data> Set(string table, string key, Data data)
            => Task.FromResult(Data.FromError(
                new SettingsError("Simulated save failure", "IOError", 500)
                { TableName = table, KeyName = key }));
        public Task<Data> Remove(string table, string key) => _inner.Remove(table, key);
        public Task<global::app.data.@this<bool>> Exists(string table, string key) => _inner.Exists(table, key);
        public Task<global::app.data.@this<List<string>>> Tables() => _inner.Tables();
        public void Dispose() { }
    }

    /// <summary>
    /// Settings store wrapper that delegates all operations except Remove, which always fails.
    /// </summary>
    private class FailingRemoveDataSource : global::app.modules.settings.IStore
    {
        private readonly global::app.modules.settings.IStore _inner;
        public FailingRemoveDataSource(global::app.modules.settings.IStore inner) => _inner = inner;

        public Task<Data> Get(string table, string key) => _inner.Get(table, key);
        public Task<Data> Get<T>(string table, string key) where T : Data => _inner.Get<T>(table, key);
        public Task<Data> GetAll(string table) => _inner.GetAll(table);
        public Task<global::app.data.@this<List<T>>> GetAll<T>(string table) where T : Data => _inner.GetAll<T>(table);
        public Task<Data> Set(string table, string key, Data data) => _inner.Set(table, key, data);
        public Task<Data> Remove(string table, string key)
            => Task.FromResult(Data.FromError(
                new SettingsError("Simulated remove failure", "IOError", 500)
                { TableName = table, KeyName = key }));
        public Task<global::app.data.@this<bool>> Exists(string table, string key) => _inner.Exists(table, key);
        public Task<global::app.data.@this<List<string>>> Tables() => _inner.Tables();
        public void Dispose() { }
    }

    /// <summary>
    /// Settings store where GetAll always returns an error.
    /// </summary>
    private class FailingGetAllDataSource : global::app.modules.settings.IStore
    {
        public Task<Data> Get(string table, string key)
            => Task.FromResult(Data.FromError(new SettingsError("Simulated failure")));
        public Task<Data> Get<T>(string table, string key) where T : Data => Get(table, key);
        public Task<Data> GetAll(string table)
            => Task.FromResult(Data.FromError(new SettingsError("Simulated GetAll failure")));
        public Task<global::app.data.@this<List<T>>> GetAll<T>(string table) where T : Data
            => Task.FromResult(global::app.data.@this<List<T>>.FromError(new SettingsError("Simulated GetAll failure")));
        public Task<Data> Set(string table, string key, Data data)
            => Task.FromResult(Data.FromError(new SettingsError("Simulated failure")));
        public Task<Data> Remove(string table, string key)
            => Task.FromResult(Data.FromError(new SettingsError("Simulated failure")));
        public Task<global::app.data.@this<bool>> Exists(string table, string key)
            => Task.FromResult(global::app.data.@this<bool>.FromError(new SettingsError("Simulated failure")));
        public Task<global::app.data.@this<List<string>>> Tables()
            => Task.FromResult(global::app.data.@this<List<string>>.FromError(new SettingsError("Simulated failure")));
        public void Dispose() { }
    }
}
