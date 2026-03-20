namespace PLang.Tests.Runtime2.Core;

/// <summary>
/// Tests ICache.TryAddAsync atomic semantics on MemoryStepCache.
/// TryAddAsync is the atomic add-if-absent operation needed for nonce replay prevention.
/// </summary>
public class CacheTryAddTests
{
    [Test]
    public async Task TryAddAsync_NewKey_ReturnsTrue()
    {
        // First add → true.
        //
        // Arrange: new MemoryStepCache()
        // Act: TryAddAsync("nonce-1", "value", settings)
        // Assert: result == true
        await Assert.Fail("stub — implementation depends on ICache.TryAddAsync");
    }

    [Test]
    public async Task TryAddAsync_ExistingKey_ReturnsFalse()
    {
        // Second add with same key → false.
        //
        // Arrange: new MemoryStepCache(), TryAddAsync("nonce-1", ...)
        // Act: TryAddAsync("nonce-1", ...) again
        // Assert: first == true, second == false
        await Assert.Fail("stub — implementation depends on ICache.TryAddAsync");
    }

    [Test]
    public async Task TryAddAsync_DifferentKeys_BothTrue()
    {
        // Two different keys → both true.
        //
        // Arrange: new MemoryStepCache()
        // Act: TryAddAsync("nonce-1"), TryAddAsync("nonce-2")
        // Assert: both return true
        await Assert.Fail("stub — implementation depends on ICache.TryAddAsync");
    }

    [Test]
    public async Task TryAddAsync_AfterExpiry_ReturnsTrue()
    {
        // Short TTL, wait, re-add → true.
        //
        // Arrange: TryAddAsync with 1-second TTL
        // Act: wait ~1.5 seconds, TryAddAsync same key
        // Assert: second call returns true (entry expired)
        await Assert.Fail("stub — implementation depends on ICache.TryAddAsync");
    }

    [Test]
    public async Task TryAddAsync_ConcurrentCalls_OnlyOneSucceeds()
    {
        // Parallel same key → exactly one true.
        //
        // Arrange: new MemoryStepCache()
        // Act: fire N parallel TryAddAsync("same-key", ...) tasks
        // Assert: exactly one returns true, rest return false
        await Assert.Fail("stub — implementation depends on ICache.TryAddAsync");
    }
}
