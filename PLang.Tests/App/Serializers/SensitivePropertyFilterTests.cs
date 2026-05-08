using System.Text.Json;
using App;
using global::App.Channels.Serializers.Serializer;
using global::App.Errors;
using global::App.modules.identity;
using PLangEngine = global::App.@this;

namespace PLang.Tests.App.Serializers;

// Record with an auto-generated ToString — used to prove AssertionError.Message
// does not fall back to value.ToString() (which would print every field, including
// [Sensitive] ones). This is the exact vector for security finding #3 on the
// junit.xml path where only Error.Message is emitted.
file sealed record LeakySecretRecord(string Name, [property: Sensitive] string Secret);

// Non-string [Sensitive] carrier — proves DiagnosticOutput still renders the key
// with a "******" placeholder instead of silently stripping the property.
file sealed class NonStringSecretCarrier
{
    public string Name { get; set; } = "";
    [Sensitive] public byte[] Key { get; set; } = Array.Empty<byte>();
}

public class SensitivePropertyFilterTests
{
    private string _tempDir = null!;
    private PLangEngine _app = null!;

    [Before(Test)]
    public void Setup()
    {
        _tempDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "plang_test_sensitive_" + Guid.NewGuid().ToString("N")[..8]);
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

        var serializer = new global::App.Channels.Serializers.Serializer.Json();
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

        // Raw JsonSerializer (used by DataSource) has no global::App.Channels.Serializers.Filters.Sensitive
        var json = JsonSerializer.Serialize(identity);

        await Assert.That(json).Contains("pubkey123");
        await Assert.That(json).Contains("secret456");
    }

    [Test]
    public async Task Sensitive_NoOpOnTypesWithoutAttribute()
    {
        // A type without [Sensitive] should serialize normally
        var obj = new { Name = "test", Value = 42 };

        var serializer = new global::App.Channels.Serializers.Serializer.Json();
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
        var serializer = new global::App.Channels.Serializers.Serializer.Json();
        var storeSerializer = serializer.ForView(View.Store);
        var storeJson = storeSerializer.Serialize(identity);

        // Identity doesn't use view attributes, so Store view serializes all non-sensitive
        await Assert.That(storeJson).DoesNotContain("secret456");
    }

    // Diagnostic output keeps the [Sensitive] key visible and replaces the value with
    // "******". Distinguishing absent / null / redacted matters when a human is reading
    // a crash dump — the key must still appear.
    [Test]
    public async Task Sensitive_DiagnosticOutput_MasksValueAsAsterisks()
    {
        var identity = new Identity
        {
            Name = "test",
            PublicKey = "pubkey123",
            PrivateKey = "secret456",
            IsDefault = true
        };

        var json = JsonSerializer.Serialize(identity, global::App.Utils.Json.DiagnosticOutput);

        await Assert.That(json).Contains("pubkey123");
        await Assert.That(json).DoesNotContain("secret456");
        await Assert.That(json).Contains("privateKey");
        await Assert.That(json).Contains("******");
    }

    // CamelCaseIndented is the storage/output format — it must NOT mask or strip,
    // otherwise .build/app.pr round-trips would lose sensitive data.
    [Test]
    public async Task Sensitive_CamelCaseIndented_KeepsSensitiveData()
    {
        var identity = new Identity
        {
            Name = "test",
            PublicKey = "pubkey123",
            PrivateKey = "secret456",
            IsDefault = true
        };

        var json = JsonSerializer.Serialize(identity, global::App.Utils.Json.CamelCaseIndented);

        await Assert.That(json).Contains("secret456");
    }

    // Regression for security finding #3, JUnit path: BuildJUnit emits only
    // run.Error?.Message — so AssertionError.Message itself must be masked,
    // not just the structured expected/actual fields in the JSON envelope.
    // A record's auto-ToString prints every field; the old FormatValue used
    // value.ToString() for non-strings and leaked the raw Secret.
    [Test]
    public async Task AssertionError_Message_MasksSensitiveViaDiagnosticOutput()
    {
        var actual = new LeakySecretRecord("alice", "topsecret-PLAINTEXT-777");
        var error = new AssertionError(expected: "nope", actual: actual);

        await Assert.That(error.Message).DoesNotContain("topsecret-PLAINTEXT-777");
        await Assert.That(error.Message).Contains("******");
        // Key name stays visible — distinguishing absent / null / redacted is
        // the contract DiagnosticOutput promises.
        await Assert.That(error.Message).Contains("secret");
    }

    // Regression for F4: non-string [Sensitive] properties used to be dropped
    // silently by Mask. DiagnosticOutput's stated intent is that the key
    // remains visible so a human reading a crash dump can distinguish
    // "not set" from "redacted". The filter now synthesizes a string-typed
    // property so the mask literal renders regardless of source type.
    [Test]
    public async Task Sensitive_NonStringProperty_RendersMaskedValueNotStripped()
    {
        var obj = new NonStringSecretCarrier { Name = "ed25519", Key = new byte[] { 0xDE, 0xAD, 0xBE, 0xEF } };

        var json = JsonSerializer.Serialize(obj, global::App.Utils.Json.DiagnosticOutput);

        await Assert.That(json).Contains("key");
        await Assert.That(json).Contains("******");
        // base64 of the bytes, never leaked
        await Assert.That(json).DoesNotContain("3q2+7w==");
        await Assert.That(json).DoesNotContain("3q2-7w");
    }

    [Test]
    public async Task Sensitive_IdentityData_PrivateKeyExcluded()
    {
        // End-to-end: create real identity, serialize, verify PrivateKey absent
        var create = new Create { Context = _app.System.Context, Name = "e2e", SetAsDefault = true };
        var result = await create.Run();
        var identity = result.Value as Identity;

        var serializer = new global::App.Channels.Serializers.Serializer.Json();
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
