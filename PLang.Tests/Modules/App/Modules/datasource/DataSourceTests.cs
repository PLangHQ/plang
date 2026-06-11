using app.actor.context;
using app.module.settings;
using app.error;
using app.variable;
using PLangEngine = global::app.@this;

namespace PLang.Tests.App.Modules.datasource;

public class DataSourceTests
{
    private string _tempDir = null!;
    private PLangEngine _app = null!;

    [Before(Test)]
    public void Setup()
    {
        _tempDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "plang_test_ds_" + Guid.NewGuid().ToString("N")[..8]);
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

    private global::app.module.settings.Sqlite CreateDataSource()
    {
        var dbPath = global::app.type.path.@this.Resolve("/.db/test.sqlite", _app.System.Context!);
        return new global::app.module.settings.Sqlite(dbPath);
    }

    [Test]
    public async Task Set_ThenGet_ReturnsValue()
    {
        using var ds = CreateDataSource();
        var setResult = await ds.Set("settings", "ApiKey", new Data("ApiKey", "sk-123"));
        await setResult.IsSuccess();

        var getResult = await ds.Get("settings", "ApiKey");
        await getResult.IsSuccess();
        await Assert.That((await getResult.Value())?.ToString()).IsEqualTo("sk-123");
    }

    [Test]
    public async Task Get_NonExistentKey_ReturnsNullValue()
    {
        using var ds = CreateDataSource();
        var result = await ds.Get("settings", "NonExistent");
        await result.IsSuccess();
        await Assert.That((await result.Value())).IsNull();
    }

    [Test]
    public async Task Set_OverwritesExistingValue()
    {
        using var ds = CreateDataSource();
        await ds.Set("settings", "ApiKey", new Data("ApiKey", "old-value"));
        await ds.Set("settings", "ApiKey", new Data("ApiKey", "new-value"));

        var result = await ds.Get("settings", "ApiKey");
        await Assert.That((await result.Value())?.ToString()).IsEqualTo("new-value");
    }

    [Test]
    public async Task Remove_DeletesKey()
    {
        using var ds = CreateDataSource();
        await ds.Set("settings", "ApiKey", new Data("ApiKey", "sk-123"));
        var removeResult = await ds.Remove("settings", "ApiKey");
        await removeResult.IsSuccess();

        var result = await ds.Get("settings", "ApiKey");
        await Assert.That((await result.Value())).IsNull();
    }

    [Test]
    public async Task Remove_NonExistentKey_Succeeds()
    {
        using var ds = CreateDataSource();
        var result = await ds.Remove("settings", "NonExistent");
        await result.IsSuccess();
    }

    [Test]
    public async Task Exists_ReturnsTrueWhenKeyExists()
    {
        using var ds = CreateDataSource();
        await ds.Set("settings", "ApiKey", new Data("ApiKey", "sk-123"));
        var result = await ds.Exists("settings", "ApiKey");
        await result.IsSuccess();
        await Assert.That((await result.Value())!.Value).IsTrue();
    }

    [Test]
    public async Task Exists_ReturnsFalseWhenKeyDoesNotExist()
    {
        using var ds = CreateDataSource();
        var result = await ds.Exists("settings", "NonExistent");
        await result.IsSuccess();
        await Assert.That((await result.Value())!.Value).IsFalse();
    }

    [Test]
    public async Task GetAll_ReturnsAllKeyValuePairs()
    {
        using var ds = CreateDataSource();
        await ds.Set("settings", "Key1", new Data("Key1", "Value1"));
        await ds.Set("settings", "Key2", new Data("Key2", "Value2"));

        var result = await ds.GetAll("settings");
        await result.IsSuccess();
        var items = global::app.type.item.@this.Lower<List<Data>>(await result.Value());
        await Assert.That(items).IsNotNull();
        await Assert.That(items!.Count).IsEqualTo(2);
    }

    [Test]
    public async Task Tables_ReturnsTableNames()
    {
        using var ds = CreateDataSource();
        await ds.Set("settings", "Key1", new Data("Key1", "Value1"));
        await ds.Set("encryption", "Key2", new Data("Key2", "Value2"));

        var result = await ds.Tables();
        await result.IsSuccess();
        var tables = result.GetValue<List<string>>();
        await Assert.That(tables).IsNotNull();
        await Assert.That(tables!.Count).IsGreaterThanOrEqualTo(2);
    }

    [Test]
    public async Task Set_NullValue_StoresAndRetrieves()
    {
        using var ds = CreateDataSource();
        await ds.Set("settings", "NullKey", new Data("NullKey", null));
        var result = await ds.Get("settings", "NullKey");
        await result.IsSuccess();
        await Assert.That((await result.Value())).IsNull();
    }

    [Test]
    public async Task Set_IntegerValue_PreservesType()
    {
        using var ds = CreateDataSource();
        await ds.Set("settings", "Count", new Data("Count", 42));
        var result = await ds.Get("settings", "Count");
        await result.IsSuccess();
        // JSON deserialization may box as long
        var value = Convert.ToInt64((await result.Value()));
        await Assert.That(value).IsEqualTo(42L);
    }

    [Test]
    public async Task MultipleTables_AreIsolated()
    {
        using var ds = CreateDataSource();
        await ds.Set("settings", "Key", new Data("Key", "SettingsValue"));
        await ds.Set("encryption", "Key", new Data("Key", "EncryptionValue"));

        var settingsResult = await ds.Get("settings", "Key");
        var encryptionResult = await ds.Get("encryption", "Key");

        await Assert.That((await settingsResult.Value())?.ToString()).IsEqualTo("SettingsValue");
        await Assert.That((await encryptionResult.Value())?.ToString()).IsEqualTo("EncryptionValue");
    }

    [Test]
    public async Task ResolveTableName_ReturnsLastNamespaceSegment()
    {
        var result = global::app.module.settings.IStore.ResolveTableName(typeof(global::app.module.settings.Set));
        await Assert.That(result).IsEqualTo("settings");
    }

    // --- SanitizeTableName tests (indirect via public API) ---

    [Test]
    public async Task Set_TableNameWithSpecialChars_StripsNonAlphanumeric()
    {
        using var ds = CreateDataSource();
        // Special chars should be stripped, leaving "settingsDROPTABLEsettings"
        var result = await ds.Set("settings; DROP TABLE settings", "Key", new Data("Key", "Value"));
        await result.IsSuccess();

        // Should be retrievable using the same dirty name (sanitized identically)
        var getResult = await ds.Get("settings; DROP TABLE settings", "Key");
        await getResult.IsSuccess();
        await Assert.That((await getResult.Value())?.ToString()).IsEqualTo("Value");
    }

    [Test]
    public async Task Set_TableNameWithUnderscores_PreservesUnderscores()
    {
        using var ds = CreateDataSource();
        var result = await ds.Set("my_table", "Key", new Data("Key", "Value"));
        await result.IsSuccess();

        var getResult = await ds.Get("my_table", "Key");
        await Assert.That((await getResult.Value())?.ToString()).IsEqualTo("Value");
    }

    [Test]
    public async Task Set_EmptyTableName_FallsBackToDefault()
    {
        using var ds = CreateDataSource();
        // All chars stripped → empty → "default_table"
        var result = await ds.Set("!!!", "Key", new Data("Key", "Value"));
        await result.IsSuccess();

        var getResult = await ds.Get("!!!", "Key");
        await Assert.That((await getResult.Value())?.ToString()).IsEqualTo("Value");
    }

    [Test]
    public async Task Set_MixedCaseTableName_NormalizesToLowercase()
    {
        using var ds = CreateDataSource();
        await ds.Set("Settings", "Key", new Data("Key", "Value1"));

        // Same name in different case should hit the same table
        var getResult = await ds.Get("settings", "Key");
        await Assert.That((await getResult.Value())?.ToString()).IsEqualTo("Value1");
    }

    // --- ClassifyException tests ---

    [Test]
    public async Task SettingsError_ClassifiesLockedDatabase()
    {
        var ex = new Exception("database is locked");
        var error = SettingsError.FromException(ex, "settings", "key");
        await Assert.That(error.Key).IsEqualTo("DatabaseLocked");
    }

    [Test]
    public async Task SettingsError_ClassifiesDiskError()
    {
        var ex = new Exception("disk I/O error");
        var error = SettingsError.FromException(ex, "settings");
        await Assert.That(error.Key).IsEqualTo("DiskError");
    }

    [Test]
    public async Task SettingsError_ClassifiesCorrupt()
    {
        var ex = new Exception("database disk image is corrupt");
        var error = SettingsError.FromException(ex);
        await Assert.That(error.Key).IsEqualTo("DatabaseCorrupt");
    }

    [Test]
    public async Task SettingsError_ClassifiesPermissionDenied()
    {
        var ex = new Exception("permission denied");
        var error = SettingsError.FromException(ex);
        await Assert.That(error.Key).IsEqualTo("PermissionDenied");
    }

    [Test]
    public async Task SettingsError_ClassifiesUnknownAsDefault()
    {
        var ex = new Exception("something unexpected");
        var error = SettingsError.FromException(ex);
        await Assert.That(error.Key).IsEqualTo("SettingsError");
        await Assert.That(error.StatusCode).IsEqualTo(500);
    }

    // --- In-memory datasource tests ---

    [Test]
    public async Task InMemory_CrudOperations()
    {
        using var ds = global::app.module.settings.Sqlite.InMemory("test_crud");

        // Set
        var setResult = await ds.Set("items", "key1", new Data("key1", "value1"));
        await setResult.IsSuccess();

        // Get
        var getResult = await ds.Get("items", "key1");
        await getResult.IsSuccess();
        await Assert.That((await getResult.Value())?.ToString()).IsEqualTo("value1");

        // Exists
        var existsResult = await ds.Exists("items", "key1");
        await Assert.That((await existsResult.Value())!.Value).IsTrue();

        // GetAll
        await ds.Set("items", "key2", new Data("key2", "value2"));
        var allResult = await ds.GetAll("items");
        var items = global::app.type.item.@this.Lower<List<Data>>(await allResult.Value());
        await Assert.That(items).IsNotNull();
        await Assert.That(items!.Count).IsEqualTo(2);

        // Remove
        var removeResult = await ds.Remove("items", "key1");
        await removeResult.IsSuccess();
        var afterRemove = await ds.Exists("items", "key1");
        await Assert.That((await afterRemove.Value())!.Value).IsFalse();
    }

    [Test]
    public async Task InMemory_SchemaPersistsAcrossOperations()
    {
        using var ds = global::app.module.settings.Sqlite.InMemory("test_schema");

        // First operation creates the table
        await ds.Set("persistent", "key1", new Data("key1", "value1"));

        // Second operation should see the same table (no re-creation needed)
        var result = await ds.Get("persistent", "key1");
        await result.IsSuccess();
        await Assert.That((await result.Value())?.ToString()).IsEqualTo("value1");

        // Tables() should list it
        var tablesResult = await ds.Tables();
        var tables = tablesResult.GetValue<List<string>>();
        await Assert.That(tables).IsNotNull();
        await Assert.That(tables!).Contains("persistent");
    }

    [Test]
    public async Task InMemory_TwoNamesAreIsolated()
    {
        using var ds1 = global::app.module.settings.Sqlite.InMemory("db_alpha");
        using var ds2 = global::app.module.settings.Sqlite.InMemory("db_beta");

        await ds1.Set("shared", "key", new Data("key", "alpha_value"));
        await ds2.Set("shared", "key", new Data("key", "beta_value"));

        var result1 = await ds1.Get("shared", "key");
        var result2 = await ds2.Get("shared", "key");

        await Assert.That((await result1.Value())?.ToString()).IsEqualTo("alpha_value");
        await Assert.That((await result2.Value())?.ToString()).IsEqualTo("beta_value");
    }

    [Test]
    public async Task InMemory_DisposeClosesDb()
    {
        // Create, populate, dispose
        var ds1 = global::app.module.settings.Sqlite.InMemory("disposable_db");
        await ds1.Set("data", "key", new Data("key", "value"));
        ds1.Dispose();

        // New datasource with same name should start empty (sentinel closed → DB vanished)
        using var ds2 = global::app.module.settings.Sqlite.InMemory("disposable_db");
        var result = await ds2.Get("data", "key");
        await result.IsSuccess();
        await Assert.That((await result.Value())).IsNull();
    }

    [Test]
    public async Task App_UsesInMemory_WhenTestingEnabled()
    {
        await using var engine = new PLangEngine(_tempDir);
        engine.Tester.IsEnabled = true;

        // app.SettingsStore is in-memory under Testing — no .db directory created.
        var ds = engine.SettingsStore;
        var setResult = await ds.Set("test_table", "k", new Data("k", "v"));
        await setResult.IsSuccess();

        var getResult = await ds.Get("test_table", "k");
        await Assert.That((await getResult.Value())?.ToString()).IsEqualTo("v");

        // Verify no .db directory was created on disk
        var dbDir = System.IO.Path.Combine(_tempDir, ".db");
        await Assert.That(System.IO.Directory.Exists(dbDir)).IsFalse();
    }

    [Test]
    public async Task App_UsesFileBacked_ByDefault()
    {
        await using var engine = new PLangEngine(_tempDir);
        // Testing not enabled → file-backed system.sqlite.

        var ds = engine.SettingsStore;
        var setResult = await ds.Set("file_table", "k", new Data("k", "v"));
        await setResult.IsSuccess();

        // Verify .db directory WAS created on disk
        var dbDir = System.IO.Path.Combine(_tempDir, ".db");
        await Assert.That(System.IO.Directory.Exists(dbDir)).IsTrue();
    }
}
