using PLang.Runtime2.Engine;
using PLang.Runtime2.Engine.Memory;
using PLang.Runtime2.modules.crypto;
using PLang.Runtime2.modules.crypto.providers;
using PLang.SafeFileSystem;

namespace PLang.Tests.Runtime2.Modules.crypto;

public class HashActionTests : IDisposable
{
    private readonly string _tempDir;
    private readonly PLangFileSystem _fs;
    private readonly PLang.Runtime2.Engine.@this _engine;

    public HashActionTests()
    {
        _tempDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "plang_test_" + Guid.NewGuid().ToString("N"));
        System.IO.Directory.CreateDirectory(_tempDir);
        _fs = new PLangFileSystem(_tempDir, "");
        _engine = new PLang.Runtime2.Engine.@this(_tempDir, fileSystem: _fs);
    }

    public void Dispose()
    {
        _engine.DisposeAsync().AsTask().GetAwaiter().GetResult();
        if (System.IO.Directory.Exists(_tempDir))
            System.IO.Directory.Delete(_tempDir, true);
    }

    // --- Hash action ---

    [Test]
    public async Task Hash_StringInput_ReturnsHashedData()
    {
        // String input → HashedData with Algorithm="keccak256", Format="json", Hash=hex string.
        //
        // Arrange: create Hash action with Data="hello", default algorithm
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
        // Assert: both produce the same Hash value (deterministic serialization)
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
    public async Task Hash_NullInput_ReturnsError()
    {
        // Null data → Data.Success == false, handler does not throw.
        //
        // Arrange: create Hash action with Data = null
        // Act: Run()
        // Assert: result.Success == false, result.Error is not null
        await Assert.Fail("stub — implementation depends on crypto module");
    }

    [Test]
    public async Task Hash_UnsupportedAlgorithm_ReturnsError()
    {
        // Unknown algorithm → Data.Success == false with meaningful error key.
        //
        // Arrange: create Hash action with Algorithm = "md5"
        // Act: Run()
        // Assert: result.Success == false, result.Error.Key contains algorithm info
        await Assert.Fail("stub — implementation depends on crypto module");
    }

    [Test]
    public async Task Hash_ProviderThrows_ReturnsDataFail()
    {
        // Provider that throws Exception → handler catches, returns Data.Fail().
        // Handler must never let exceptions propagate.
        //
        // Arrange: register mock provider that throws InvalidOperationException
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
        // Act: verify with same data and hash via Verify action
        // Assert: result.Success == true, result.Value == true
        await Assert.Fail("stub — implementation depends on crypto module");
    }

    [Test]
    public async Task Verify_WrongHash_ReturnsFalse()
    {
        // Correct data but wrong hash → Data.Ok(false), not an error.
        //
        // Arrange: hash "hello", then verify "hello" against a different hash
        // Act: Verify action
        // Assert: result.Success == true, result.Value == false
        await Assert.Fail("stub — implementation depends on crypto module");
    }

    [Test]
    public async Task Verify_CorruptedHashString_ReturnsError()
    {
        // Non-hex garbage as hash → Data.Success == false (not a crash).
        //
        // Arrange: create Verify action with Hash = "not-a-valid-hex-string!!!"
        // Act: Run()
        // Assert: result.Success == false, result.Error is not null
        await Assert.Fail("stub — implementation depends on crypto module");
    }

    [Test]
    public async Task Verify_NullInput_ReturnsError()
    {
        // Null data → Data.Success == false, handler does not throw.
        //
        // Arrange: create Verify action with Data = null, Hash = "abc123"
        // Act: Run()
        // Assert: result.Success == false, result.Error is not null
        await Assert.Fail("stub — implementation depends on crypto module");
    }

    [Test]
    public async Task Verify_ProviderThrows_ReturnsDataFail()
    {
        // Provider that throws → handler catches, returns Data.Fail().
        //
        // Arrange: register mock provider that throws on Verify
        // Act: verify via action handler
        // Assert: result.Success == false, result.Error.Exception is not null
        await Assert.Fail("stub — implementation depends on crypto module");
    }
}
