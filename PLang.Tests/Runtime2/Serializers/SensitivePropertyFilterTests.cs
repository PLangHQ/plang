using System.Text.Json;
using PLang.Runtime2.Engine;
using PLang.Runtime2.Engine.Channels.Serializers.Serializer;
using PLang.Runtime2.modules.identity;
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
        var identity = new IdentityVariable
        {
            Name = "test",
            PublicKey = "pubkey123",
            PrivateKey = "secret456",
            IsDefault = true,
            IsArchived = false,
            Created = DateTime.UtcNow
        };

        var serializer = new JsonStreamSerializer();
        var json = serializer.Serialize(identity);

        await Assert.That(json).Contains("pubkey123");
        await Assert.That(json).DoesNotContain("secret456");
        await Assert.That(json).Contains("test");
    }

    [Test]
    public async Task Sensitive_IncludedInRawJsonSerializer()
    {
        var identity = new IdentityVariable
        {
            Name = "test",
            PublicKey = "pubkey123",
            PrivateKey = "secret456",
            IsDefault = true
        };

        // Raw JsonSerializer (used by DataSource) has no SensitivePropertyFilter
        var json = JsonSerializer.Serialize(identity);

        await Assert.That(json).Contains("pubkey123");
        await Assert.That(json).Contains("secret456");
    }

    [Test]
    public async Task Sensitive_NoOpOnTypesWithoutAttribute()
    {
        // A type without [Sensitive] should serialize normally
        var obj = new { Name = "test", Value = 42 };

        var serializer = new JsonStreamSerializer();
        var json = serializer.Serialize(obj);

        await Assert.That(json).Contains("test");
        await Assert.That(json).Contains("42");
    }

    [Test]
    public async Task Sensitive_WorksAlongsideViewAttributes()
    {
        var identity = new IdentityVariable
        {
            Name = "test",
            PublicKey = "pubkey123",
            PrivateKey = "secret456",
            IsDefault = true
        };

        // ForView should also strip [Sensitive] in addition to view filtering
        var serializer = new JsonStreamSerializer();
        var storeSerializer = serializer.ForView(View.Store);
        var storeJson = storeSerializer.Serialize(identity);

        // IdentityVariable doesn't use view attributes, so Store view serializes all non-sensitive
        await Assert.That(storeJson).DoesNotContain("secret456");
    }

    [Test]
    public async Task Sensitive_IdentityVariable_PrivateKeyExcluded()
    {
        // End-to-end: create real identity, serialize, verify PrivateKey absent
        var create = new Create { Context = _engine.System.Context, Name = "e2e", SetAsDefault = true };
        var result = await create.Run();
        var identity = result.Value as IdentityVariable;

        var serializer = new JsonStreamSerializer();
        var json = serializer.Serialize(identity);

        await Assert.That(json).Contains(identity!.PublicKey);
        await Assert.That(json).DoesNotContain(identity.PrivateKey);
        await Assert.That(json).Contains("e2e");
    }
}
