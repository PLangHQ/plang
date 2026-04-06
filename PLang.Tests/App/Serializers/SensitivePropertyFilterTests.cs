using System.Text.Json;
using App.Engine;
using App.Engine.Channels.Serializers.Serializer;
using App.modules.identity;
using PLangEngine = App.Engine.@this;

namespace PLang.Tests.App.Serializers;

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
        var identity = new Identity
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
        var identity = new Identity
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
        var identity = new Identity
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

        // Identity doesn't use view attributes, so Store view serializes all non-sensitive
        await Assert.That(storeJson).DoesNotContain("secret456");
    }

    [Test]
    public async Task Sensitive_IdentityData_PrivateKeyExcluded()
    {
        // End-to-end: create real identity, serialize, verify PrivateKey absent
        var create = new Create { Context = _engine.System.Context, Name = "e2e", SetAsDefault = true };
        var result = await create.Run();
        var identity = result as Identity;

        var serializer = new JsonStreamSerializer();
        var json = serializer.Serialize(identity);

        // Deserialize back to check values — raw Contains() fails when base64 '+' is escaped to '\u002B'
        var deserialized = JsonSerializer.Deserialize<JsonElement>(json);
        await Assert.That(deserialized.GetProperty("publicKey").GetString()).IsEqualTo(identity!.PublicKey);
        await Assert.That(json).DoesNotContain(identity.PrivateKey);
        // Also check the escaped form of PrivateKey isn't present
        var escapedPrivateKey = JsonSerializer.Serialize(identity.PrivateKey).Trim('"');
        await Assert.That(json).DoesNotContain(escapedPrivateKey);
        await Assert.That(deserialized.GetProperty("name").GetString()).IsEqualTo("e2e");
    }
}
