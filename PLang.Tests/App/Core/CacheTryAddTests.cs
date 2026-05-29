using app.modules.cache;
using app.goal.steps.step;
using app.variable;

namespace PLang.Tests.App.Core;

/// <summary>
/// Tests ICache.TryAddAsync atomic semantics on global::app.modules.cache.memory.
/// TryAddAsync is the atomic add-if-absent operation needed for nonce replay prevention.
/// </summary>
public class CacheTryAddTests
{
    private static CacheSettings MakeSettings(long durationMs = 300_000)
        => new() { DurationMs = durationMs, Sliding = false };

    [Test]
    public async Task TryAddAsync_NewKey_ReturnsTrue()
    {
        var cache = new global::app.modules.cache.Memory();

        var result = await cache.TryAddAsync("nonce-1", Data.Ok("value"), MakeSettings());

        await Assert.That(result).IsTrue();
    }

    [Test]
    public async Task TryAddAsync_ExistingKey_ReturnsFalse()
    {
        var cache = new global::app.modules.cache.Memory();
        var settings = MakeSettings();

        var first = await cache.TryAddAsync("nonce-1", Data.Ok("value1"), settings);
        var second = await cache.TryAddAsync("nonce-1", Data.Ok("value2"), settings);

        await Assert.That(first).IsTrue();
        await Assert.That(second).IsFalse();
    }

    [Test]
    public async Task TryAddAsync_DifferentKeys_BothTrue()
    {
        var cache = new global::app.modules.cache.Memory();
        var settings = MakeSettings();

        var result1 = await cache.TryAddAsync("nonce-1", Data.Ok("value1"), settings);
        var result2 = await cache.TryAddAsync("nonce-2", Data.Ok("value2"), settings);

        await Assert.That(result1).IsTrue();
        await Assert.That(result2).IsTrue();
    }

    [Test]
    public async Task TryAddAsync_AfterExpiry_ReturnsTrue()
    {
        var cache = new global::app.modules.cache.Memory();
        var settings = MakeSettings(durationMs: 1000);

        var first = await cache.TryAddAsync("nonce-1", Data.Ok("value"), settings);
        await Assert.That(first).IsTrue();

        await Task.Delay(1500);

        var second = await cache.TryAddAsync("nonce-1", Data.Ok("value"), settings);
        await Assert.That(second).IsTrue();
    }

    [Test]
    public async Task TryAddAsync_ConcurrentCalls_OnlyOneSucceeds()
    {
        var cache = new global::app.modules.cache.Memory();
        var settings = MakeSettings();

        var tasks = Enumerable.Range(0, 10)
            .Select(_ => cache.TryAddAsync("same-key", Data.Ok("value"), settings))
            .ToArray();

        var results = await Task.WhenAll(tasks);

        var trueCount = results.Count(r => r);
        await Assert.That(trueCount).IsEqualTo(1);
    }
}
