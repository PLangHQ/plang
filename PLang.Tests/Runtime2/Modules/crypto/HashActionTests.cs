using PLang.Runtime2.Engine.Context;
using PLang.Runtime2.Engine.Errors;
using PLang.Runtime2.Engine.Memory;
using PLang.Runtime2.modules.crypto;
using PLang.Runtime2.modules.crypto.providers;
using PLangEngine = PLang.Runtime2.Engine.@this;

namespace PLang.Tests.Runtime2.Modules.crypto;

public class HashActionTests
{
    private string _tempDir = null!;
    private PLangEngine _engine = null!;

    [Before(Test)]
    public void Setup()
    {
        _tempDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "plang_test_crypto_" + Guid.NewGuid().ToString("N")[..8]);
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

    // --- Hash action ---

    [Test]
    public async Task Hash_StringInput_ReturnsHashedData()
    {
        // String input → HashedData with Algorithm="keccak256", Format="json", Hash=hex string.
        //
        // Arrange: create Hash action with Context=Ctx, Data="hello", default algorithm
        // Act: Run()
        // Assert: result.Success, result.Value is HashedData,
        //         hashedData.Algorithm == "keccak256",
        //         hashedData.Format == "json",
        //         hashedData.Hash is non-empty hex string
        await Assert.Fail("stub — implementation depends on crypto module");
    }

    [Test]
    public async Task Hash_ObjectInput_SerializesToJsonBeforeHashing()
    {
        // Object is JSON-serialized before hashing → deterministic for same object.
        //
        // Arrange: create Hash action with Data = new { Name = "test", Value = 42 }
        // Act: Run() twice with identical object
        // Assert: both produce the same HashedData.Hash value (deterministic serialization)
        await Assert.Fail("stub — implementation depends on crypto module");
    }

    [Test]
    public async Task Hash_ByteArrayInput_FormatIsRaw()
    {
        // byte[] input → Format="raw", no JSON serialization.
        //
        // Arrange: create Hash action with Data = new byte[] { 1, 2, 3 }
        // Act: Run()
        // Assert: result.Value is HashedData with Format == "raw"
        await Assert.Fail("stub — implementation depends on crypto module");
    }

    [Test]
    public async Task Hash_ExplicitAlgorithm_OverridesDefault()
    {
        // Passing algorithm="sha256" should produce SHA256 output even though
        // the default algorithm is keccak256. Tests action parameter plumbing.
        // Note: crypto hash has no per-call Provider param (unlike signing).
        //
        // Arrange: create Hash action with Data="test", Algorithm="sha256"
        // Act: Run()
        // Assert: HashedData.Algorithm == "sha256",
        //         HashedData.Hash matches known SHA256("test") hex
        await Assert.Fail("stub — implementation depends on crypto module");
    }

    [Test]
    public async Task Hash_NullInput_ReturnsError()
    {
        // Null data → Data.FromError(new ActionError(..., "ValidationError", 400)).
        // Handler must not throw.
        //
        // Arrange: create Hash action with Data = null
        // Act: Run()
        // Assert: result.Success == false, result.Error is ActionError
        await Assert.Fail("stub — implementation depends on crypto module");
    }

    [Test]
    public async Task Hash_UnsupportedAlgorithm_ReturnsError()
    {
        // Unknown algorithm → Data.FromError with meaningful error key.
        // Provider throws NotSupportedException, handler catches and wraps.
        //
        // Arrange: create Hash action with Algorithm = "md5"
        // Act: Run()
        // Assert: result.Success == false, result.Error.Key indicates the problem
        await Assert.Fail("stub — implementation depends on crypto module");
    }

    [Test]
    public async Task Hash_ProviderThrows_ReturnsDataFail()
    {
        // Provider that throws Exception → handler catches, returns Data.FromError().
        // Handler must never let exceptions propagate.
        //
        // Arrange: register mock ICryptoProvider that throws InvalidOperationException
        // Act: hash via action handler
        // Assert: result.Success == false, result.Error.Exception is InvalidOperationException
        await Assert.Fail("stub — implementation depends on crypto module");
    }

    // --- Verify action ---

    [Test]
    public async Task Verify_RoundTrip_ReturnsTrue()
    {
        // Hash via hash action, then verify via verify action → Data.Ok(true).
        //
        // Arrange: hash "hello" via Hash action, get HashedData
        // Act: verify with same data and HashedData.Hash via Verify action
        // Assert: result.Success == true, result.Value == true
        await Assert.Fail("stub — implementation depends on crypto module");
    }

    [Test]
    public async Task Verify_WrongHash_ReturnsFalse()
    {
        // Correct data but wrong hash → Data.Ok(false), not an error.
        //
        // Arrange: hash "hello", then verify "hello" against a different hash string
        // Act: Verify action
        // Assert: result.Success == true, result.Value == false
        await Assert.Fail("stub — implementation depends on crypto module");
    }

    [Test]
    public async Task Verify_CorruptedHashString_ReturnsError()
    {
        // Non-hex garbage as hash → Data.FromError (not a crash).
        //
        // Arrange: create Verify action with Hash = "not-a-valid-hex-string!!!"
        // Act: Run()
        // Assert: result.Success == false, result.Error is not null
        await Assert.Fail("stub — implementation depends on crypto module");
    }

    [Test]
    public async Task Verify_NullInput_ReturnsError()
    {
        // Null data → Data.FromError(new ActionError(..., "ValidationError", 400)).
        // Handler must not throw.
        //
        // Arrange: create Verify action with Data = null, Hash = "abc123"
        // Act: Run()
        // Assert: result.Success == false, result.Error is ActionError
        await Assert.Fail("stub — implementation depends on crypto module");
    }

    [Test]
    public async Task Verify_ProviderThrows_ReturnsDataFail()
    {
        // Provider that throws → handler catches, returns Data.FromError().
        //
        // Arrange: register mock ICryptoProvider that throws on Verify
        // Act: verify via action handler
        // Assert: result.Success == false, result.Error.Exception is not null
        await Assert.Fail("stub — implementation depends on crypto module");
    }
}
