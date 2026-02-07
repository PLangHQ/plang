using PLang.Runtime2.Context;

namespace PLang.Tests.Runtime2.Context;

public class PLangAppContextTests
{
    [Test]
    public async Task Constructor_SetsProperties()
    {
        using var context = new PLangAppContext("/app/path", "development");

        await Assert.That(context.RootPath).IsEqualTo("/app/path");
        await Assert.That(context.Environment).IsEqualTo("development");
    }

    [Test]
    public async Task Constructor_DefaultsToProduction()
    {
        using var context = new PLangAppContext("/app/path");

        await Assert.That(context.Environment).IsEqualTo("production");
    }

    [Test]
    public async Task Constructor_GeneratesId()
    {
        using var context = new PLangAppContext("/app");

        await Assert.That(context.Id).IsNotNull();
        await Assert.That(context.Id.Length).IsEqualTo(12);
    }

    [Test]
    public async Task Constructor_SetsStartedAt()
    {
        var before = DateTime.UtcNow;

        using var context = new PLangAppContext("/app");

        var after = DateTime.UtcNow;
        await Assert.That(context.StartedAt).IsGreaterThanOrEqualTo(before);
        await Assert.That(context.StartedAt).IsLessThanOrEqualTo(after);
    }

    [Test]
    public async Task Constructor_CreatesEvents()
    {
        using var context = new PLangAppContext("/app");

        await Assert.That(context.Events).IsNotNull();
    }

    [Test]
    public async Task Constructor_CreatesSerializerRegistry()
    {
        using var context = new PLangAppContext("/app");

        await Assert.That(context.Serializers).IsNotNull();
    }

    [Test]
    public async Task IsDebugMode_DefaultsFalse()
    {
        using var context = new PLangAppContext("/app");

        await Assert.That(context.IsDebugMode).IsFalse();
    }

    [Test]
    public async Task IsDebugMode_CanBeSet()
    {
        using var context = new PLangAppContext("/app");

        context.IsDebugMode = true;

        await Assert.That(context.IsDebugMode).IsTrue();
    }

    [Test]
    public async Task Environment_CanBeChanged()
    {
        using var context = new PLangAppContext("/app");

        context.Environment = "staging";

        await Assert.That(context.Environment).IsEqualTo("staging");
    }

    [Test]
    public async Task Indexer_SetsAndGetsValue()
    {
        using var context = new PLangAppContext("/app");

        context["key"] = "value";

        await Assert.That(context["key"]).IsEqualTo("value");
    }

    [Test]
    public async Task Indexer_SetNull_RemovesKey()
    {
        using var context = new PLangAppContext("/app");
        context["key"] = "value";

        context["key"] = null;

        await Assert.That(context["key"]).IsNull();
    }

    [Test]
    public async Task Indexer_NonexistentKey_ReturnsNull()
    {
        using var context = new PLangAppContext("/app");

        await Assert.That(context["nonexistent"]).IsNull();
    }

    [Test]
    public async Task Indexer_CaseInsensitive()
    {
        using var context = new PLangAppContext("/app");
        context["Key"] = "value";

        await Assert.That(context["key"]).IsEqualTo("value");
        await Assert.That(context["KEY"]).IsEqualTo("value");
    }

    [Test]
    public async Task Get_ReturnsTypedValue()
    {
        using var context = new PLangAppContext("/app");
        context["count"] = 42;

        var value = context.Get<int>("count");

        await Assert.That(value).IsEqualTo(42);
    }

    [Test]
    public async Task Get_NonexistentKey_ReturnsDefault()
    {
        using var context = new PLangAppContext("/app");

        var value = context.Get<int>("nonexistent");

        await Assert.That(value).IsEqualTo(0);
    }

    [Test]
    public async Task Get_WrongType_ReturnsDefault()
    {
        using var context = new PLangAppContext("/app");
        context["value"] = "not a number";

        var value = context.Get<int>("value");

        await Assert.That(value).IsEqualTo(0);
    }

    [Test]
    public async Task Set_StoresTypedValue()
    {
        using var context = new PLangAppContext("/app");

        context.Set("count", 42);

        await Assert.That(context.Get<int>("count")).IsEqualTo(42);
    }

    [Test]
    public async Task Set_Null_RemovesKey()
    {
        using var context = new PLangAppContext("/app");
        context.Set<string>("key", "value");

        context.Set<string>("key", null!);

        await Assert.That(context["key"]).IsNull();
    }

    [Test]
    public async Task GetOrCreate_ExistingKey_ReturnsExisting()
    {
        using var context = new PLangAppContext("/app");
        var original = new List<int> { 1, 2, 3 };
        context["list"] = original;

        var result = context.GetOrCreate("list", () => new List<int> { 4, 5, 6 });

        await Assert.That(result).IsEqualTo(original);
    }

    [Test]
    public async Task GetOrCreate_NonexistentKey_CreatesNew()
    {
        using var context = new PLangAppContext("/app");

        var result = context.GetOrCreate("list", () => new List<int> { 1, 2, 3 });

        await Assert.That(result).IsNotNull();
        await Assert.That(result.Count).IsEqualTo(3);
    }

    [Test]
    public async Task GetOrCreate_CreatedValue_IsStored()
    {
        using var context = new PLangAppContext("/app");

        var created = context.GetOrCreate("list", () => new List<int> { 1, 2, 3 });
        var retrieved = context.GetOrCreate("list", () => new List<int> { 4, 5, 6 });

        await Assert.That(retrieved).IsEqualTo(created);
    }

    [Test]
    public async Task ContainsKey_ExistingKey_ReturnsTrue()
    {
        using var context = new PLangAppContext("/app");
        context["key"] = "value";

        await Assert.That(context.ContainsKey("key")).IsTrue();
    }

    [Test]
    public async Task ContainsKey_NonexistentKey_ReturnsFalse()
    {
        using var context = new PLangAppContext("/app");

        await Assert.That(context.ContainsKey("nonexistent")).IsFalse();
    }

    [Test]
    public async Task Remove_ExistingKey_ReturnsTrue()
    {
        using var context = new PLangAppContext("/app");
        context["key"] = "value";

        var removed = context.Remove("key");

        await Assert.That(removed).IsTrue();
        await Assert.That(context.ContainsKey("key")).IsFalse();
    }

    [Test]
    public async Task Remove_NonexistentKey_ReturnsFalse()
    {
        using var context = new PLangAppContext("/app");

        var removed = context.Remove("nonexistent");

        await Assert.That(removed).IsFalse();
    }

    [Test]
    public async Task Keys_ReturnsAllKeys()
    {
        using var context = new PLangAppContext("/app");
        context["key1"] = "value1";
        context["key2"] = "value2";

        var keys = context.Keys.ToList();

        await Assert.That(keys).Contains("key1");
        await Assert.That(keys).Contains("key2");
    }

    [Test]
    public async Task ShutdownToken_IsNotCancelledInitially()
    {
        using var context = new PLangAppContext("/app");

        await Assert.That(context.ShutdownToken.IsCancellationRequested).IsFalse();
    }

    [Test]
    public async Task RequestShutdown_CancelsToken()
    {
        using var context = new PLangAppContext("/app");

        context.RequestShutdown();

        await Assert.That(context.ShutdownToken.IsCancellationRequested).IsTrue();
    }

    [Test]
    public async Task Uptime_ReturnsPositiveTimeSpan()
    {
        using var context = new PLangAppContext("/app");
        await Task.Delay(10);

        var uptime = context.Uptime;

        await Assert.That(uptime.TotalMilliseconds).IsGreaterThan(0);
    }

    [Test]
    public async Task Dispose_CancelsShutdownToken()
    {
        var context = new PLangAppContext("/app");
        var token = context.ShutdownToken;

        context.Dispose();

        await Assert.That(token.IsCancellationRequested).IsTrue();
    }

    [Test]
    public async Task Dispose_ClearsData()
    {
        var context = new PLangAppContext("/app");
        context["key"] = "value";

        context.Dispose();

        await Assert.That(context.Keys.Any()).IsFalse();
    }

    [Test]
    public async Task Dispose_DisposesDisposableValues()
    {
        var context = new PLangAppContext("/app");
        var disposable = new TestDisposable();
        context["disposable"] = disposable;

        context.Dispose();

        await Assert.That(disposable.IsDisposed).IsTrue();
    }

    [Test]
    public async Task Dispose_CalledTwice_DoesNotThrow()
    {
        var context = new PLangAppContext("/app");

        context.Dispose();
        context.Dispose();

        await Assert.That(context.Keys.Any()).IsFalse();
    }

    private class TestDisposable : IDisposable
    {
        public bool IsDisposed { get; private set; }

        public void Dispose()
        {
            IsDisposed = true;
        }
    }
}
