using System.Reflection;
using PLang.Runtime2.Engine.Context;
using PLang.Runtime2.Engine.Settings;
using PLang.Runtime2.Engine.Errors;
using PLang.Runtime2.Engine.Memory;
using PLang.Runtime2.modules.identity;
using PLangEngine = PLang.Runtime2.Engine.@this;

namespace PLang.Tests.Runtime2.Modules.identity;

/// <summary>
/// Tests for identity module error paths:
/// - GetOrCreateDefaultAsync promote/auto-create save failures (via Get action)
/// - Handler catch blocks (export.cs, get.cs, IdentityData.cs)
/// - Handler save/remove failures (create, setDefault, rename, archive, unarchive)
/// - LoadAllAsync when DataSource.GetAll fails (via GetAll action)
/// - Deserialize with unrecognized value types (via Get action)
/// </summary>
public class IdentityErrorPathTests
{
    private string _tempDir = null!;
    private PLangEngine _engine = null!;

    [Before(Test)]
    public void Setup()
    {
        _tempDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "plang_test_iderr_" + Guid.NewGuid().ToString("N")[..8]);
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

    // --- GetOrCreateDefaultAsync: auto-create save failure (via Get action) ---

    [Test]
    public async Task GetOrCreateDefault_AutoCreateSaveFails_ReturnsError()
    {
        // No identities exist → auto-create path → save fails
        SwapDataSource(_engine.System, new FailingSaveDataSource(
            _engine.System.SettingsStore));

        var result = await new Get { Context = Ctx, Name = null }.Run();
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
        SwapDataSource(_engine.System, new FailingSaveDataSource(
            _engine.System.SettingsStore));

        var result = await new Get { Context = Ctx, Name = null }.Run();
        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error!.Key).IsEqualTo("IOError");
    }

    // --- Handler catch blocks for save failures ---

    [Test]
    public async Task Get_NullName_SaveFails_ReturnsError()
    {
        // Swap to failing save — Get(null) calls GetOrCreateDefaultAsync which returns error
        SwapDataSource(_engine.System, new FailingSaveDataSource(
            _engine.System.SettingsStore));

        var handler = new Get { Context = Ctx, Name = null };
        var result = await handler.Run();

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error).IsNotNull();
        await Assert.That(result.Error!.Key).IsEqualTo("IOError");
    }

    [Test]
    public async Task Export_NullName_SaveFails_ReturnsError()
    {
        // Swap to failing save — Export(null) calls GetOrCreateDefaultAsync which returns error
        SwapDataSource(_engine.System, new FailingSaveDataSource(
            _engine.System.SettingsStore));

        var handler = new Export { Context = Ctx, Name = null };
        var result = await handler.Run();

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error).IsNotNull();
        await Assert.That(result.Error!.Key).IsEqualTo("IOError");
    }

    [Test]
    public async Task IdentityData_ResolveDefault_SaveFails_Throws()
    {
        // Swap to failing save before IdentityData resolves
        SwapDataSource(_engine.System, new FailingSaveDataSource(
            _engine.System.SettingsStore));

        // Create a fresh IdentityData that hasn't resolved yet
        var identityData = new IdentityData(_engine);

        // Access Value triggers ResolveDefault → GetOrCreateDefaultAsync fails → throws
        try
        {
            _ = identityData.Value;
            Assert.Fail("Expected InvalidOperationException");
        }
        catch (InvalidOperationException ex)
        {
            await Assert.That(ex.Message).Contains("Identity resolution failed");
            await Assert.That(ex.Message).Contains("IOError");
        }
    }

    // --- Handler save/remove failure paths ---

    [Test]
    public async Task Create_ClearDefaultSaveFails_ReturnsError()
    {
        // Create an existing default identity
        var h = new Create { Context = Ctx, Name = "existing", SetAsDefault = true };
        await h.Run();

        // Swap to failing save — clearing old default fails
        SwapDataSource(_engine.System, new FailingSaveDataSource(
            _engine.System.SettingsStore));

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
        SwapDataSource(_engine.System, new FailingSaveDataSource(
            _engine.System.SettingsStore));

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
        SwapDataSource(_engine.System, new FailingSaveDataSource(
            _engine.System.SettingsStore));

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
        SwapDataSource(_engine.System, new FailingSaveDataSource(
            _engine.System.SettingsStore));

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
        SwapDataSource(_engine.System, new FailingSaveDataSource(
            _engine.System.SettingsStore));

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
        SwapDataSource(_engine.System, new FailingRemoveDataSource(
            _engine.System.SettingsStore));

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
        SwapDataSource(_engine.System, new FailingSaveDataSource(
            _engine.System.SettingsStore));

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
        SwapDataSource(_engine.System, new FailingSaveDataSource(
            _engine.System.SettingsStore));

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
        SwapDataSource(_engine.System, new FailingGetAllDataSource());

        var handler = new list { Context = Ctx };
        var result = await handler.Run();
        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error).IsNotNull();
    }

    // --- Deserialize with unrecognized value type (via Get action) ---

    [Test]
    public async Task Get_UnrecognizedValueType_ReturnsNotFound()
    {
        // Store a raw integer in the identity table — Deserialize won't recognize it
        var ds = _engine.System.SettingsStore;
        await ds.Set("identity", "weird", new Data("weird", 42));

        var result = await new Get { Context = Ctx, Name = "weird" }.Run();
        // Provider's LoadAsync returns null for unrecognized types → Get returns NotFound
        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error!.Key).IsEqualTo("NotFound");
    }

    [Test]
    public async Task GetAll_MixedValues_SkipsUnrecognized()
    {
        var ds = _engine.System.SettingsStore;

        // Store a valid identity via Create action
        var create = new Create { Context = Ctx, Name = "valid", SetAsDefault = true };
        await create.Run();

        // Store an unrecognizable value directly in DataSource
        await ds.Set("identity", "garbage", new Data("garbage", "just a string"));

        var handler = new list { Context = Ctx };
        var result = await handler.Run();
        await Assert.That(result.Success).IsTrue();

        var list = result.Value as List<IdentityVariable>;
        // Should contain the valid identity but skip the garbage
        await Assert.That(list!.Count).IsEqualTo(1);
        await Assert.That(list[0].Name).IsEqualTo("valid");
    }

    // --- Helpers ---

    /// <summary>
    /// Swaps the DataSource on an Actor via reflection.
    /// Required because Actor._dataSource is private readonly Lazy&lt;ISettingsStore&gt;.
    /// </summary>
    private static void SwapDataSource(Actor actor, ISettingsStore newDataSource)
    {
        var field = typeof(Actor).GetField("_dataSource",
            BindingFlags.NonPublic | BindingFlags.Instance);
        field!.SetValue(actor, new Lazy<ISettingsStore>(() => newDataSource));
    }

    /// <summary>
    /// DataSource wrapper that delegates all operations except Set, which always fails.
    /// </summary>
    private class FailingSaveDataSource : ISettingsStore
    {
        private readonly ISettingsStore _inner;
        public FailingSaveDataSource(ISettingsStore inner) => _inner = inner;

        public Task<Data> Get(string table, string key) => _inner.Get(table, key);
        public Task<Data> Get<T>(string table, string key) where T : Data => _inner.Get<T>(table, key);
        public Task<Data> GetAll(string table) => _inner.GetAll(table);
        public Task<Data> GetAll<T>(string table) where T : Data => _inner.GetAll<T>(table);
        public Task<Data> Set(string table, string key, Data data)
            => Task.FromResult(Data.FromError(
                new DataSourceError("Simulated save failure", "IOError", 500)
                { TableName = table, KeyName = key }));
        public Task<Data> Remove(string table, string key) => _inner.Remove(table, key);
        public Task<Data> Exists(string table, string key) => _inner.Exists(table, key);
        public Task<Data> Tables() => _inner.Tables();
        public void Dispose() { }
    }

    /// <summary>
    /// Settings store wrapper that delegates all operations except Remove, which always fails.
    /// </summary>
    private class FailingRemoveDataSource : ISettingsStore
    {
        private readonly ISettingsStore _inner;
        public FailingRemoveDataSource(ISettingsStore inner) => _inner = inner;

        public Task<Data> Get(string table, string key) => _inner.Get(table, key);
        public Task<Data> Get<T>(string table, string key) where T : Data => _inner.Get<T>(table, key);
        public Task<Data> GetAll(string table) => _inner.GetAll(table);
        public Task<Data> GetAll<T>(string table) where T : Data => _inner.GetAll<T>(table);
        public Task<Data> Set(string table, string key, Data data) => _inner.Set(table, key, data);
        public Task<Data> Remove(string table, string key)
            => Task.FromResult(Data.FromError(
                new DataSourceError("Simulated remove failure", "IOError", 500)
                { TableName = table, KeyName = key }));
        public Task<Data> Exists(string table, string key) => _inner.Exists(table, key);
        public Task<Data> Tables() => _inner.Tables();
        public void Dispose() { }
    }

    /// <summary>
    /// Settings store where GetAll always returns an error.
    /// </summary>
    private class FailingGetAllDataSource : ISettingsStore
    {
        public Task<Data> Get(string table, string key)
            => Task.FromResult(Data.FromError(new DataSourceError("Simulated failure")));
        public Task<Data> Get<T>(string table, string key) where T : Data => Get(table, key);
        public Task<Data> GetAll(string table)
            => Task.FromResult(Data.FromError(new DataSourceError("Simulated GetAll failure")));
        public Task<Data> GetAll<T>(string table) where T : Data => GetAll(table);
        public Task<Data> Set(string table, string key, Data data)
            => Task.FromResult(Data.FromError(new DataSourceError("Simulated failure")));
        public Task<Data> Remove(string table, string key)
            => Task.FromResult(Data.FromError(new DataSourceError("Simulated failure")));
        public Task<Data> Exists(string table, string key)
            => Task.FromResult(Data.FromError(new DataSourceError("Simulated failure")));
        public Task<Data> Tables()
            => Task.FromResult(Data.FromError(new DataSourceError("Simulated failure")));
        public void Dispose() { }
    }
}
