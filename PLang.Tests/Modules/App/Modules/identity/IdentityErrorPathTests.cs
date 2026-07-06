using System.Reflection;
using app.actor.context;
using app.module.setting;
using app.error;
using app.variable;
using app.module.identity;
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
        _app = global::PLang.Tests.TestApp.Plain(_tempDir);
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
            await _app.SettingsStore));

        var getHandler = new global::app.module.identity.Get(Ctx) { Name = null };
        await getHandler.Attach(null, Ctx);
        var result = await getHandler.Run();
        await result.IsFailure();
        await Assert.That(result.Error!.Key).IsEqualTo("IOError");
    }

    [Test]
    public async Task GetOrCreateDefault_PromoteSaveFails_ReturnsError()
    {
        // Create a non-default, non-archived identity first (using real DataSource)
        var create = new Create(Ctx) { Name = (global::app.type.text.@this)"candidate", SetAsDefault = (global::app.type.@bool.@this)false };
        await create.Attach(null, Ctx);
        var createResult = await create.Run();
        await createResult.IsSuccess();

        // Now swap to failing DataSource — GetAll still works (delegates), but Set fails
        SwapDataSource(_app, new FailingSaveDataSource(
            await _app.SettingsStore));

        var getHandler = new global::app.module.identity.Get(Ctx) { Name = null };
        await getHandler.Attach(null, Ctx);
        var result = await getHandler.Run();
        await result.IsFailure();
        await Assert.That(result.Error!.Key).IsEqualTo("IOError");
    }

    // --- Handler catch blocks for save failures ---

    [Test]
    public async Task Get_NullName_SaveFails_ReturnsError()
    {
        // Swap to failing save — Get(null) calls GetOrCreateDefaultAsync which returns error
        SwapDataSource(_app, new FailingSaveDataSource(
            await _app.SettingsStore));

        var handler = new global::app.module.identity.Get(Ctx) { Name = null };
        await handler.Attach(null, Ctx);
        var result = await handler.Run();

        await result.IsFailure();
        await Assert.That(result.Error).IsNotNull();
        await Assert.That(result.Error!.Key).IsEqualTo("IOError");
    }

    [Test]
    public async Task Export_NullName_SaveFails_ReturnsError()
    {
        // Swap to failing save — Export(null) calls GetOrCreateDefaultAsync which returns error
        SwapDataSource(_app, new FailingSaveDataSource(
            await _app.SettingsStore));

        var handler = new Export(Ctx) { Name = null };
        await handler.Attach(null, Ctx);
        var result = await handler.Run();

        await result.IsFailure();
        await Assert.That(result.Error).IsNotNull();
        await Assert.That(result.Error!.Key).IsEqualTo("IOError");
    }

    [Test]
    public async Task MyIdentity_ResolveDefault_SaveFails_ReturnsNull()
    {
        // Swap to failing save before %MyIdentity% resolves
        SwapDataSource(_app, new FailingSaveDataSource(
            await _app.SettingsStore));

        // Access %MyIdentity% — the computed cell calls the provider, which
        // fails; the answer is the present-null VALUE (the singleton).
        var data = await _app.User.Context.Variable.Get("MyIdentity");
        await Assert.That(data).IsNotNull();
        var v = await data!.Value();
        await Assert.That(v is null or global::app.type.@null.@this).IsTrue();
    }

    // --- Handler save/remove failure paths ---

    [Test]
    public async Task Create_ClearDefaultSaveFails_ReturnsError()
    {
        // Create an existing default identity
        var h = new Create(Ctx) { Name = (global::app.type.text.@this)"existing", SetAsDefault = (global::app.type.@bool.@this)true };
        await h.Attach(null, Ctx);
        await h.Run();

        // Swap to failing save — clearing old default fails
        SwapDataSource(_app, new FailingSaveDataSource(
            await _app.SettingsStore));

        var handler = new Create(Ctx) { Name = (global::app.type.text.@this)"new", SetAsDefault = (global::app.type.@bool.@this)true };
        await handler.Attach(null, Ctx);
        var result = await handler.Run();

        await result.IsFailure();
        await Assert.That(result.Error).IsNotNull();
        await Assert.That(result.Error!.Key).IsEqualTo("IOError");
    }

    [Test]
    public async Task Create_SaveNewIdentityFails_ReturnsError()
    {
        // Swap to failing save — saving the new identity fails
        SwapDataSource(_app, new FailingSaveDataSource(
            await _app.SettingsStore));

        var handler = new Create(Ctx) { Name = (global::app.type.text.@this)"newid", SetAsDefault = (global::app.type.@bool.@this)false };
        await handler.Attach(null, Ctx);
        var result = await handler.Run();

        await result.IsFailure();
        await Assert.That(result.Error).IsNotNull();
        await Assert.That(result.Error!.Key).IsEqualTo("IOError");
    }

    [Test]
    public async Task SetDefault_ClearOldDefaultSaveFails_ReturnsError()
    {
        // Create two identities: one default, one not
        var h1 = new Create(Ctx) { Name = (global::app.type.text.@this)"old", SetAsDefault = (global::app.type.@bool.@this)true };
        await h1.Attach(null, Ctx);
        await h1.Run();
        var h2 = new Create(Ctx) { Name = (global::app.type.text.@this)"new", SetAsDefault = (global::app.type.@bool.@this)false };
        await h2.Attach(null, Ctx);
        await h2.Run();

        // Swap to failing save — clearing old default fails
        SwapDataSource(_app, new FailingSaveDataSource(
            await _app.SettingsStore));

        var handler = new SetDefault(Ctx) { Name = (global::app.type.text.@this)"new" };
        await handler.Attach(null, Ctx);
        var result = await handler.Run();

        await result.IsFailure();
        await Assert.That(result.Error).IsNotNull();
        await Assert.That(result.Error!.Key).IsEqualTo("IOError");
    }

    [Test]
    public async Task SetDefault_SaveNewDefaultFails_ReturnsError()
    {
        // Create a single non-default identity (no existing defaults to clear)
        var h = new Create(Ctx) { Name = (global::app.type.text.@this)"target", SetAsDefault = (global::app.type.@bool.@this)false };
        await h.Attach(null, Ctx);
        await h.Run();

        // Swap to failing save — saving the new default fails
        SwapDataSource(_app, new FailingSaveDataSource(
            await _app.SettingsStore));

        var handler = new SetDefault(Ctx) { Name = (global::app.type.text.@this)"target" };
        await handler.Attach(null, Ctx);
        var result = await handler.Run();

        await result.IsFailure();
        await Assert.That(result.Error).IsNotNull();
        await Assert.That(result.Error!.Key).IsEqualTo("IOError");
    }

    [Test]
    public async Task Rename_SaveNewNameFails_ReturnsError()
    {
        var h = new Create(Ctx) { Name = (global::app.type.text.@this)"oldname", SetAsDefault = (global::app.type.@bool.@this)false };
        await h.Attach(null, Ctx);
        await h.Run();

        // Swap to failing save — saving with new name fails
        SwapDataSource(_app, new FailingSaveDataSource(
            await _app.SettingsStore));

        var handler = new Rename(Ctx) { Name = (global::app.type.text.@this)"oldname", NewName = (global::app.type.text.@this)"newname" };
        await handler.Attach(null, Ctx);
        var result = await handler.Run();

        await result.IsFailure();
        await Assert.That(result.Error).IsNotNull();
        await Assert.That(result.Error!.Key).IsEqualTo("IOError");
    }

    [Test]
    public async Task Rename_RemoveOldNameFails_ReturnsError()
    {
        var h = new Create(Ctx) { Name = (global::app.type.text.@this)"oldname", SetAsDefault = (global::app.type.@bool.@this)false };
        await h.Attach(null, Ctx);
        await h.Run();

        // Swap to failing remove — save succeeds but remove fails
        SwapDataSource(_app, new FailingRemoveDataSource(
            await _app.SettingsStore));

        var handler = new Rename(Ctx) { Name = (global::app.type.text.@this)"oldname", NewName = (global::app.type.text.@this)"newname" };
        await handler.Attach(null, Ctx);
        var result = await handler.Run();

        await result.IsFailure();
        await Assert.That(result.Error).IsNotNull();
        await Assert.That(result.Error!.Key).IsEqualTo("IOError");
    }

    [Test]
    public async Task Archive_SaveFails_ReturnsError()
    {
        var h = new Create(Ctx) { Name = (global::app.type.text.@this)"toarchive", SetAsDefault = (global::app.type.@bool.@this)false };
        await h.Attach(null, Ctx);
        await h.Run();

        // Swap to failing save
        SwapDataSource(_app, new FailingSaveDataSource(
            await _app.SettingsStore));

        var handler = new Archive(Ctx) { Name = (global::app.type.text.@this)"toarchive" };
        await handler.Attach(null, Ctx);
        var result = await handler.Run();

        await result.IsFailure();
        await Assert.That(result.Error).IsNotNull();
        await Assert.That(result.Error!.Key).IsEqualTo("IOError");
    }

    [Test]
    public async Task Unarchive_SaveFails_ReturnsError()
    {
        // Create and archive an identity
        var h = new Create(Ctx) { Name = (global::app.type.text.@this)"tounarchive", SetAsDefault = (global::app.type.@bool.@this)false };
        await h.Attach(null, Ctx);
        await h.Run();
        var archiveH = new Archive(Ctx) { Name = (global::app.type.text.@this)"tounarchive" };
        await archiveH.Attach(null, Ctx);
        await archiveH.Run();

        // Swap to failing save
        SwapDataSource(_app, new FailingSaveDataSource(
            await _app.SettingsStore));

        var handler = new Unarchive(Ctx) { Name = (global::app.type.text.@this)"tounarchive" };
        await handler.Attach(null, Ctx);
        var result = await handler.Run();

        await result.IsFailure();
        await Assert.That(result.Error).IsNotNull();
        await Assert.That(result.Error!.Key).IsEqualTo("IOError");
    }

    // --- LoadAllAsync when DataSource.GetAll fails (via GetAll action) ---

    [Test]
    public async Task GetAll_DataSourceFails_ReturnsError()
    {
        SwapDataSource(_app, new FailingGetAllDataSource());

        var handler = new list(Ctx);
        await handler.Attach(null, Ctx);
        var result = await handler.Run();
        await result.IsFailure();
        await Assert.That(result.Error).IsNotNull();
    }

    // --- Store-direct read of a corrupt entry ---

    [Test]
    public async Task StoreGet_NonIdentityEntry_DeclinesOnLift()
    {
        // A module author wanting the identity calls store.Get<Identity>(...).Value()
        // directly (the identity.Get action is a thin forwarder over this seam). The
        // store hands back a typed FACE with no processing; a corrupt entry (a raw
        // number, not an identity) surfaces its decline only when the developer LIFTS
        // it — never inside the store.
        var ds = await _app.SettingsStore;
        await ds.Set("identity", "weird", new Data("weird", 42, context: Ctx));

        var data   = await ds.Get<Identity>("identity", "weird");
        var loaded = await data.Value();

        await Assert.That((object?)loaded).IsNull();
        await Assert.That(data.Error!.Key).IsEqualTo("CreateItemDeclined");
    }

    [Test]
    public async Task GetAll_SkipsUndeserializableEntries()
    {
        var ds = await _app.SettingsStore;

        // Store a valid identity via Create action
        var create = new Create(Ctx) { Name = (global::app.type.text.@this)"valid", SetAsDefault = (global::app.type.@bool.@this)true };
        await create.Attach(null, Ctx);
        await create.Run();

        // Store a non-identity value directly — not a dict, so it can't deserialize to an Identity
        await ds.Set("identity", "garbage", new Data("garbage", "just a string", context: Ctx));

        var handler = new list(Ctx);
        await handler.Attach(null, Ctx);
        var result = await handler.Run();
        await result.IsSuccess();

        var list = result.GetValue<List<Identity>>();
        // Only the valid identity is listed; the corrupt entry is skipped, not degraded to empty.
        await Assert.That(list!.Count).IsEqualTo(1);
    }

    // --- Helpers ---

    /// <summary>
    /// Swaps the SettingsStore on App via reflection on the auto-property
    /// backing field. After stage 13's settings rework the store moved from
    /// per-actor to app-level — there's a single shared <c>app.SettingsStore</c>.
    /// </summary>
    private static void SwapDataSource(global::app.@this app, global::app.module.setting.IStore newDataSource)
    {
        var field = typeof(global::app.@this).GetField("_settingsStore",
            BindingFlags.NonPublic | BindingFlags.Instance);
        field!.SetValue(app, new Lazy<System.Threading.Tasks.Task<global::app.module.setting.IStore>>(
            () => System.Threading.Tasks.Task.FromResult(newDataSource)));
    }

    /// <summary>
    /// DataSource wrapper that delegates all operations except Set, which always fails.
    /// </summary>
    private class FailingSaveDataSource : global::app.module.setting.IStore
    {
        private readonly global::app.module.setting.IStore _inner;
        public FailingSaveDataSource(global::app.module.setting.IStore inner) => _inner = inner;

        public Task<global::app.data.@this<T>> Get<T>(string table, string key) where T : global::app.type.item.@this, global::app.type.item.ICreate<T> => _inner.Get<T>(table, key);
        public Task<global::app.data.@this<global::app.type.list.@this>> GetAll<T>(string table) where T : global::app.type.item.@this, global::app.type.item.ICreate<T> => _inner.GetAll<T>(table);
        public Task<Data> Set(string table, string key, Data data)
            => Task.FromResult(Data.FromError(
                new SettingsError("Simulated save failure", "IOError", 500)
                { TableName = table, KeyName = key }));
        public Task<Data> Remove(string table, string key) => _inner.Remove(table, key);
        public Task<global::app.data.@this<global::app.type.@bool.@this>> Exists(string table, string key) => _inner.Exists(table, key);
        public Task<global::app.data.@this<global::app.type.list.@this>> Tables() => _inner.Tables();
        public void Dispose() { }
    }

    /// <summary>
    /// Settings store wrapper that delegates all operations except Remove, which always fails.
    /// </summary>
    private class FailingRemoveDataSource : global::app.module.setting.IStore
    {
        private readonly global::app.module.setting.IStore _inner;
        public FailingRemoveDataSource(global::app.module.setting.IStore inner) => _inner = inner;

        public Task<global::app.data.@this<T>> Get<T>(string table, string key) where T : global::app.type.item.@this, global::app.type.item.ICreate<T> => _inner.Get<T>(table, key);
        public Task<global::app.data.@this<global::app.type.list.@this>> GetAll<T>(string table) where T : global::app.type.item.@this, global::app.type.item.ICreate<T> => _inner.GetAll<T>(table);
        public Task<Data> Set(string table, string key, Data data) => _inner.Set(table, key, data);
        public Task<Data> Remove(string table, string key)
            => Task.FromResult(Data.FromError(
                new SettingsError("Simulated remove failure", "IOError", 500)
                { TableName = table, KeyName = key }));
        public Task<global::app.data.@this<global::app.type.@bool.@this>> Exists(string table, string key) => _inner.Exists(table, key);
        public Task<global::app.data.@this<global::app.type.list.@this>> Tables() => _inner.Tables();
        public void Dispose() { }
    }

    /// <summary>
    /// Settings store where GetAll always returns an error.
    /// </summary>
    private class FailingGetAllDataSource : global::app.module.setting.IStore
    {
        public Task<Data> Get(string table, string key)
            => Task.FromResult(Data.FromError(new SettingsError("Simulated failure")));
        public Task<global::app.data.@this<T>> Get<T>(string table, string key) where T : global::app.type.item.@this, global::app.type.item.ICreate<T>
            => Task.FromResult(global::app.data.@this<T>.FromError(new SettingsError("Simulated failure")));
        public Task<Data> GetAll(string table)
            => Task.FromResult(Data.FromError(new SettingsError("Simulated GetAll failure")));
        public Task<global::app.data.@this<global::app.type.list.@this>> GetAll<T>(string table) where T : global::app.type.item.@this, global::app.type.item.ICreate<T>
            => Task.FromResult(global::app.data.@this<global::app.type.list.@this>.FromError(new SettingsError("Simulated GetAll failure")));
        public Task<Data> Set(string table, string key, Data data)
            => Task.FromResult(Data.FromError(new SettingsError("Simulated failure")));
        public Task<Data> Remove(string table, string key)
            => Task.FromResult(Data.FromError(new SettingsError("Simulated failure")));
        public Task<global::app.data.@this<global::app.type.@bool.@this>> Exists(string table, string key)
            => Task.FromResult(global::app.data.@this<global::app.type.@bool.@this>.FromError(new SettingsError("Simulated failure")));
        public Task<global::app.data.@this<global::app.type.list.@this>> Tables()
            => Task.FromResult(global::app.data.@this<global::app.type.list.@this>.FromError(new SettingsError("Simulated failure")));
        public void Dispose() { }
    }
}
