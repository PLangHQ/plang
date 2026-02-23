using PLang.Runtime2.Engine.Context;
using PLang.Runtime2.Engine.DataSource;
using PLang.Runtime2.Engine.Errors;
using PLang.Runtime2.Engine.Memory;
using PLangEngine = PLang.Runtime2.Engine.@this;

namespace PLang.Tests.Runtime2.Modules.datasource;

public class DataSourceTests
{
    private string _tempDir = null!;
    private PLangEngine _engine = null!;

    [Before(Test)]
    public void Setup()
    {
        _tempDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "plang_test_ds_" + Guid.NewGuid().ToString("N")[..8]);
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

    private SqliteDataSource CreateDataSource()
    {
        var dbPath = _engine.FileSystem.Path.Combine(_tempDir, ".db", "test.sqlite");
        return new SqliteDataSource(dbPath, _engine.FileSystem);
    }

    [Test]
    public async Task Set_ThenGet_ReturnsValue()
    {
        using var ds = CreateDataSource();
        var setResult = await ds.Set("settings", "ApiKey", "sk-123");
        await Assert.That(setResult.Success).IsTrue();

        var getResult = await ds.Get("settings", "ApiKey");
        await Assert.That(getResult.Success).IsTrue();
        await Assert.That(getResult.Value?.ToString()).IsEqualTo("sk-123");
    }

    [Test]
    public async Task Get_NonExistentKey_ReturnsNullValue()
    {
        using var ds = CreateDataSource();
        var result = await ds.Get("settings", "NonExistent");
        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.Value).IsNull();
    }

    [Test]
    public async Task Set_OverwritesExistingValue()
    {
        using var ds = CreateDataSource();
        await ds.Set("settings", "ApiKey", "old-value");
        await ds.Set("settings", "ApiKey", "new-value");

        var result = await ds.Get("settings", "ApiKey");
        await Assert.That(result.Value?.ToString()).IsEqualTo("new-value");
    }

    [Test]
    public async Task Remove_DeletesKey()
    {
        using var ds = CreateDataSource();
        await ds.Set("settings", "ApiKey", "sk-123");
        var removeResult = await ds.Remove("settings", "ApiKey");
        await Assert.That(removeResult.Success).IsTrue();

        var result = await ds.Get("settings", "ApiKey");
        await Assert.That(result.Value).IsNull();
    }

    [Test]
    public async Task Remove_NonExistentKey_Succeeds()
    {
        using var ds = CreateDataSource();
        var result = await ds.Remove("settings", "NonExistent");
        await Assert.That(result.Success).IsTrue();
    }

    [Test]
    public async Task Exists_ReturnsTrueWhenKeyExists()
    {
        using var ds = CreateDataSource();
        await ds.Set("settings", "ApiKey", "sk-123");
        var result = await ds.Exists("settings", "ApiKey");
        await Assert.That(result.Success).IsTrue();
        await Assert.That((bool)result.Value!).IsTrue();
    }

    [Test]
    public async Task Exists_ReturnsFalseWhenKeyDoesNotExist()
    {
        using var ds = CreateDataSource();
        var result = await ds.Exists("settings", "NonExistent");
        await Assert.That(result.Success).IsTrue();
        await Assert.That((bool)result.Value!).IsFalse();
    }

    [Test]
    public async Task GetAll_ReturnsAllKeyValuePairs()
    {
        using var ds = CreateDataSource();
        await ds.Set("settings", "Key1", "Value1");
        await ds.Set("settings", "Key2", "Value2");

        var result = await ds.GetAll("settings");
        await Assert.That(result.Success).IsTrue();
        var items = result.Value as List<Data>;
        await Assert.That(items).IsNotNull();
        await Assert.That(items!.Count).IsEqualTo(2);
    }

    [Test]
    public async Task Tables_ReturnsTableNames()
    {
        using var ds = CreateDataSource();
        await ds.Set("settings", "Key1", "Value1");
        await ds.Set("encryption", "Key2", "Value2");

        var result = await ds.Tables();
        await Assert.That(result.Success).IsTrue();
        var tables = result.Value as List<string>;
        await Assert.That(tables).IsNotNull();
        await Assert.That(tables!.Count).IsGreaterThanOrEqualTo(2);
    }

    [Test]
    public async Task Set_NullValue_StoresAndRetrieves()
    {
        using var ds = CreateDataSource();
        await ds.Set("settings", "NullKey", null);
        var result = await ds.Get("settings", "NullKey");
        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.Value).IsNull();
    }

    [Test]
    public async Task Set_IntegerValue_PreservesType()
    {
        using var ds = CreateDataSource();
        await ds.Set("settings", "Count", 42);
        var result = await ds.Get("settings", "Count");
        await Assert.That(result.Success).IsTrue();
        // JSON deserialization may box as long
        var value = Convert.ToInt64(result.Value);
        await Assert.That(value).IsEqualTo(42L);
    }

    [Test]
    public async Task MultipleTables_AreIsolated()
    {
        using var ds = CreateDataSource();
        await ds.Set("settings", "Key", "SettingsValue");
        await ds.Set("encryption", "Key", "EncryptionValue");

        var settingsResult = await ds.Get("settings", "Key");
        var encryptionResult = await ds.Get("encryption", "Key");

        await Assert.That(settingsResult.Value?.ToString()).IsEqualTo("SettingsValue");
        await Assert.That(encryptionResult.Value?.ToString()).IsEqualTo("EncryptionValue");
    }

    [Test]
    public async Task ResolveTableName_ReturnsLastNamespaceSegment()
    {
        var result = IDataSource.ResolveTableName(typeof(PLang.Runtime2.actions.settings.Set));
        await Assert.That(result).IsEqualTo("settings");
    }
}
