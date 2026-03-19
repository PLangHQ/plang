using PLang.Runtime2.Engine.Context;
using PLang.Runtime2.Engine.Memory;
using PLangEngine = PLang.Runtime2.Engine.@this;

namespace PLang.Tests.Runtime2.Serializers;

public class SensitivePropertyFilterTests
{
    private string _tempDir = null!;
    private PLangEngine _engine = null!;

    [Before(Test)]
    public void Setup()
    {
        _tempDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "plang_test_sensitive_" + Guid.NewGuid().ToString("N")[..8]);
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

    [Test]
    public async Task Sensitive_ExcludedFromJsonStreamSerializer()
    {
        // Property with [Sensitive] attribute is NOT present in JsonStreamSerializer output
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task Sensitive_IncludedInRawJsonSerializer()
    {
        // Raw JsonSerializer.Serialize() includes [Sensitive] property (DataSource storage path)
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task Sensitive_NoOpOnTypesWithoutAttribute()
    {
        // Types without [Sensitive] properties serialize normally through both paths
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task Sensitive_WorksAlongsideViewAttributes()
    {
        // [Sensitive] + [Store] or other view attributes coexist correctly
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task Sensitive_IdentityVariable_PrivateKeyExcluded()
    {
        // End-to-end: serialize IdentityVariable, PrivateKey absent in output, other fields present
        Assert.Fail("Not implemented");
    }
}
