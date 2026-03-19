using System.Reflection;
using PLang.Runtime2.Engine.Context;
using PLang.Runtime2.Engine.DataSource;
using PLang.Runtime2.Engine.Errors;
using PLang.Runtime2.Engine.Memory;
using PLang.Runtime2.modules.identity;
using PLangEngine = PLang.Runtime2.Engine.@this;

namespace PLang.Tests.Runtime2.Modules.identity;

/// <summary>
/// Tests for identity module error paths identified by tester v2:
/// - GetOrCreateDefaultAsync promote/auto-create save failures
/// - Handler catch blocks (export.cs, get.cs, IdentityData.cs)
/// - LoadAllAsync when DataSource.GetAll fails
/// - Deserialize with unrecognized value types
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

    // --- GetOrCreateDefaultAsync: auto-create save failure ---

    [Test]
    public async Task GetOrCreateDefault_AutoCreateSaveFails_ThrowsInvalidOperationException()
    {
        // No identities exist → auto-create path → save fails
        SwapDataSource(_engine.System, new FailingSaveDataSource(
            _engine.System.DataSource));

        try
        {
            await IdentityVariable.GetOrCreateDefaultAsync(_engine);
            // Should not reach here
            Assert.Fail("Expected InvalidOperationException");
        }
        catch (InvalidOperationException ex)
        {
            await Assert.That(ex.Message).Contains("Failed to save auto-created default identity");
        }
    }

    [Test]
    public async Task GetOrCreateDefault_PromoteSaveFails_ThrowsInvalidOperationException()
    {
        // Create a non-default, non-archived identity first (using real DataSource)
        var create = new Create { Context = Ctx, Name = "candidate", SetAsDefault = false };
        var createResult = await create.Run();
        await Assert.That(createResult.Success).IsTrue();

        // Now swap to failing DataSource — GetAll still works (delegates), but Set fails
        SwapDataSource(_engine.System, new FailingSaveDataSource(
            _engine.System.DataSource));

        try
        {
            await IdentityVariable.GetOrCreateDefaultAsync(_engine);
            Assert.Fail("Expected InvalidOperationException");
        }
        catch (InvalidOperationException ex)
        {
            await Assert.That(ex.Message).Contains("Failed to promote identity 'candidate' to default");
        }
    }

    // --- Handler catch blocks for InvalidOperationException ---

    [Test]
    public async Task Get_NullName_SaveFails_ReturnsSaveError()
    {
        // Swap to failing save — Get(null) calls GetOrCreateDefaultAsync which throws
        SwapDataSource(_engine.System, new FailingSaveDataSource(
            _engine.System.DataSource));

        var handler = new Get { Context = Ctx, Name = null };
        var result = await handler.Run();

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error).IsNotNull();
        await Assert.That(result.Error!.Key).IsEqualTo("SaveError");
        await Assert.That(result.Error.StatusCode).IsEqualTo(500);
    }

    [Test]
    public async Task Export_NullName_SaveFails_ReturnsSaveError()
    {
        // Swap to failing save — Export(null) calls GetOrCreateDefaultAsync which throws
        SwapDataSource(_engine.System, new FailingSaveDataSource(
            _engine.System.DataSource));

        var handler = new Export { Context = Ctx, Name = null };
        var result = await handler.Run();

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error).IsNotNull();
        await Assert.That(result.Error!.Key).IsEqualTo("SaveError");
        await Assert.That(result.Error.StatusCode).IsEqualTo(500);
    }

    [Test]
    public async Task IdentityData_ResolveDefault_SaveFails_ReturnsNull()
    {
        // Swap to failing save before IdentityData resolves
        SwapDataSource(_engine.System, new FailingSaveDataSource(
            _engine.System.DataSource));

        // Create a fresh IdentityData that hasn't resolved yet
        var identityData = new IdentityData(_engine);

        // Access Value triggers ResolveDefault → GetOrCreateDefaultAsync throws → caught → null
        var value = identityData.Value;
        await Assert.That(value).IsNull();
    }

    // --- LoadAllAsync when DataSource.GetAll fails ---

    [Test]
    public async Task LoadAllAsync_DataSourceFails_ReturnsEmptyList()
    {
        SwapDataSource(_engine.System, new FailingGetAllDataSource());

        var result = await IdentityVariable.LoadAllAsync(_engine);
        await Assert.That(result).IsNotNull();
        await Assert.That(result.Count).IsEqualTo(0);
    }

    // --- Deserialize with unrecognized value type ---

    [Test]
    public async Task LoadAsync_UnrecognizedValueType_ReturnsNull()
    {
        // Store a raw integer in the identity table — Deserialize won't recognize it
        var ds = _engine.System.DataSource;
        await ds.Set("identity", "weird", 42);

        var result = await IdentityVariable.LoadAsync(_engine, "weird");
        await Assert.That(result).IsNull();
    }

    [Test]
    public async Task LoadAllAsync_MixedValues_SkipsUnrecognized()
    {
        var ds = _engine.System.DataSource;

        // Store a valid identity
        var identity = new IdentityVariable
        {
            Name = "valid",
            PublicKey = "dGVzdA==",
            PrivateKey = "dGVzdA==",
            IsDefault = true,
            IsArchived = false,
            Created = DateTime.UtcNow
        };
        await identity.SaveAsync(_engine);

        // Store an unrecognizable value
        await ds.Set("identity", "garbage", "just a string");

        var all = await IdentityVariable.LoadAllAsync(_engine);
        // Should contain the valid identity but skip the garbage
        await Assert.That(all.Count).IsEqualTo(1);
        await Assert.That(all[0].Name).IsEqualTo("valid");
    }

    // --- Helpers ---

    /// <summary>
    /// Swaps the DataSource on an Actor via reflection.
    /// Required because Actor._dataSource is private readonly Lazy&lt;IDataSource&gt;.
    /// </summary>
    private static void SwapDataSource(Actor actor, IDataSource newDataSource)
    {
        var field = typeof(Actor).GetField("_dataSource",
            BindingFlags.NonPublic | BindingFlags.Instance);
        field!.SetValue(actor, new Lazy<IDataSource>(() => newDataSource));
    }

    /// <summary>
    /// DataSource wrapper that delegates all operations except Set, which always fails.
    /// Used to trigger save-failure paths in GetOrCreateDefaultAsync.
    /// </summary>
    private class FailingSaveDataSource : IDataSource
    {
        private readonly IDataSource _inner;
        public FailingSaveDataSource(IDataSource inner) => _inner = inner;

        public Task<Data> Get(string table, string key) => _inner.Get(table, key);
        public Task<Data> GetAll(string table) => _inner.GetAll(table);
        public Task<Data> Set(string table, string key, object? value)
            => Task.FromResult(Data.FromError(
                new DataSourceError("Simulated save failure", "IOError", 500)
                { TableName = table, KeyName = key }));
        public Task<Data> Remove(string table, string key) => _inner.Remove(table, key);
        public Task<Data> Exists(string table, string key) => _inner.Exists(table, key);
        public Task<Data> Tables() => _inner.Tables();
        public void Dispose() { } // Don't dispose inner — test cleanup handles it
    }

    /// <summary>
    /// DataSource where GetAll always returns an error.
    /// Used to test LoadAllAsync error path.
    /// </summary>
    private class FailingGetAllDataSource : IDataSource
    {
        public Task<Data> Get(string table, string key)
            => Task.FromResult(Data.FromError(new DataSourceError("Simulated failure")));
        public Task<Data> GetAll(string table)
            => Task.FromResult(Data.FromError(new DataSourceError("Simulated GetAll failure")));
        public Task<Data> Set(string table, string key, object? value)
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
