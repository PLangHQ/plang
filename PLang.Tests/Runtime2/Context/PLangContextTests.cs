using PLang.Runtime2.Engine.Context;
using PLang.Runtime2.Engine;
using PLang.Runtime2.Engine.Memory;

namespace PLang.Tests.Runtime2.Context;

public class PLangContextTests
{
    [Test]
    public async Task Constructor_SetsProperties()
    {
        await using var engine = new PLang.Runtime2.Engine.@this("/app");
        using var context = new PLangContext(engine);

        await Assert.That(context.Engine).IsEqualTo(engine);
        await Assert.That(context.MemoryStack).IsNotNull();
        await Assert.That(context.Parent).IsNull();
    }

    [Test]
    public async Task Constructor_GeneratesId()
    {
        await using var engine = new PLang.Runtime2.Engine.@this("/app");
        using var context = new PLangContext(engine);

        await Assert.That(context.Id).IsNotNull();
        await Assert.That(context.Id.Length).IsEqualTo(12);
    }

    [Test]
    public async Task Constructor_SetsCreatedAt()
    {
        await using var engine = new PLang.Runtime2.Engine.@this("/app");
        var before = DateTime.UtcNow;

        using var context = new PLangContext(engine);

        var after = DateTime.UtcNow;
        await Assert.That(context.CreatedAt).IsGreaterThanOrEqualTo(before);
        await Assert.That(context.CreatedAt).IsLessThanOrEqualTo(after);
    }

    [Test]
    public async Task Constructor_AcceptsCustomMemoryStack()
    {
        await using var engine = new PLang.Runtime2.Engine.@this("/app");
        var memoryStack = new MemoryStack();
        memoryStack.Set("test", "value");

        using var context = new PLangContext(engine, memoryStack);

        await Assert.That(context.MemoryStack).IsEqualTo(memoryStack);
    }

    [Test]
    public async Task Constructor_WithParent_SetsParent()
    {
        await using var engine = new PLang.Runtime2.Engine.@this("/app");
        using var parent = new PLangContext(engine);

        using var child = new PLangContext(engine, parent: parent);

        await Assert.That(child.Parent).IsEqualTo(parent);
    }

    [Test]
    public async Task CallStack_DefaultsToNull()
    {
        await using var engine = new PLang.Runtime2.Engine.@this("/app");
        using var context = new PLangContext(engine);

        await Assert.That(context.CallStack).IsNull();
    }

    [Test]
    public async Task CallStack_CanBeSet()
    {
        await using var engine = new PLang.Runtime2.Engine.@this("/app");
        using var context = new PLangContext(engine);
        var callStack = new CallStack();

        context.CallStack = callStack;

        await Assert.That(context.CallStack).IsEqualTo(callStack);
    }

    [Test]
    public async Task IsAsync_DefaultsFalse()
    {
        await using var engine = new PLang.Runtime2.Engine.@this("/app");
        using var context = new PLangContext(engine);

        await Assert.That(context.IsAsync).IsFalse();
    }

    [Test]
    public async Task IsAsync_CanBeSet()
    {
        await using var engine = new PLang.Runtime2.Engine.@this("/app");
        using var context = new PLangContext(engine);

        context.IsAsync = true;

        await Assert.That(context.IsAsync).IsTrue();
    }

    [Test]
    public async Task CancellationToken_LinkedToAppShutdown()
    {
        await using var engine = new PLang.Runtime2.Engine.@this("/app");
        using var context = new PLangContext(engine);

        engine.RequestShutdown();

        await Assert.That(context.CancellationToken.IsCancellationRequested).IsTrue();
    }

    [Test]
    public async Task Indexer_SetsAndGetsValue()
    {
        await using var engine = new PLang.Runtime2.Engine.@this("/app");
        using var context = new PLangContext(engine);

        context["key"] = "value";

        await Assert.That(context["key"]).IsEqualTo("value");
    }

    [Test]
    public async Task Indexer_SetNull_RemovesKey()
    {
        await using var engine = new PLang.Runtime2.Engine.@this("/app");
        using var context = new PLangContext(engine);
        context["key"] = "value";

        context["key"] = null;

        await Assert.That(context["key"]).IsNull();
    }

    [Test]
    public async Task Indexer_CaseInsensitive()
    {
        await using var engine = new PLang.Runtime2.Engine.@this("/app");
        using var context = new PLangContext(engine);
        context["Key"] = "value";

        await Assert.That(context["key"]).IsEqualTo("value");
        await Assert.That(context["KEY"]).IsEqualTo("value");
    }

    [Test]
    public async Task Get_ReturnsTypedValue()
    {
        await using var engine = new PLang.Runtime2.Engine.@this("/app");
        using var context = new PLangContext(engine);
        context["count"] = 42;

        var value = context.Get<int>("count");

        await Assert.That(value).IsEqualTo(42);
    }

    [Test]
    public async Task Get_NonexistentKey_ReturnsDefault()
    {
        await using var engine = new PLang.Runtime2.Engine.@this("/app");
        using var context = new PLangContext(engine);

        var value = context.Get<int>("nonexistent");

        await Assert.That(value).IsEqualTo(0);
    }

    [Test]
    public async Task Set_StoresTypedValue()
    {
        await using var engine = new PLang.Runtime2.Engine.@this("/app");
        using var context = new PLangContext(engine);

        context.Set("count", 42);

        await Assert.That(context.Get<int>("count")).IsEqualTo(42);
    }

    [Test]
    public async Task Set_Null_RemovesKey()
    {
        await using var engine = new PLang.Runtime2.Engine.@this("/app");
        using var context = new PLangContext(engine);
        context.Set<string>("key", "value");

        context.Set<string>("key", null!);

        await Assert.That(context["key"]).IsNull();
    }

    [Test]
    public async Task ContainsKey_ExistingKey_ReturnsTrue()
    {
        await using var engine = new PLang.Runtime2.Engine.@this("/app");
        using var context = new PLangContext(engine);
        context["key"] = "value";

        await Assert.That(context.ContainsKey("key")).IsTrue();
    }

    [Test]
    public async Task ContainsKey_NonexistentKey_ReturnsFalse()
    {
        await using var engine = new PLang.Runtime2.Engine.@this("/app");
        using var context = new PLangContext(engine);

        await Assert.That(context.ContainsKey("nonexistent")).IsFalse();
    }

    [Test]
    public async Task CreateChild_CreatesWithClonedMemoryStack()
    {
        await using var engine = new PLang.Runtime2.Engine.@this("/app");
        using var parent = new PLangContext(engine);
        parent.MemoryStack.Set("test", "value");

        using var child = parent.CreateChild();

        await Assert.That(child.MemoryStack.GetValue("test")).IsEqualTo("value");
        await Assert.That(child.MemoryStack).IsNotEqualTo(parent.MemoryStack);
    }

    [Test]
    public async Task CreateChild_SetsParent()
    {
        await using var engine = new PLang.Runtime2.Engine.@this("/app");
        using var parent = new PLangContext(engine);

        using var child = parent.CreateChild();

        await Assert.That(child.Parent).IsEqualTo(parent);
    }

    [Test]
    public async Task CreateChild_AcceptsCustomMemoryStack()
    {
        await using var engine = new PLang.Runtime2.Engine.@this("/app");
        using var parent = new PLangContext(engine);
        var customStack = new MemoryStack();
        customStack.Set("custom", "value");

        using var child = parent.CreateChild(customStack);

        await Assert.That(child.MemoryStack).IsEqualTo(customStack);
    }

    [Test]
    public async Task Clone_CreatesIndependentCopy()
    {
        await using var engine = new PLang.Runtime2.Engine.@this("/app");
        using var original = new PLangContext(engine);
        original["key"] = "value";
        original.IsAsync = true;

        using var clone = original.Clone();

        await Assert.That(clone.Get<string>("key")).IsEqualTo("value");
        await Assert.That(clone.IsAsync).IsTrue();
    }

    [Test]
    public async Task Clone_IndependentData()
    {
        await using var engine = new PLang.Runtime2.Engine.@this("/app");
        using var original = new PLangContext(engine);
        original["key"] = "value";

        using var clone = original.Clone();
        clone["key"] = "modified";

        await Assert.That(original.Get<string>("key")).IsEqualTo("value");
        await Assert.That(clone.Get<string>("key")).IsEqualTo("modified");
    }

    [Test]
    public async Task Clone_AcceptsCustomMemoryStack()
    {
        await using var engine = new PLang.Runtime2.Engine.@this("/app");
        using var original = new PLangContext(engine);
        var customStack = new MemoryStack();
        customStack.Set("custom", "value");

        using var clone = original.Clone(customStack);

        await Assert.That(clone.MemoryStack).IsEqualTo(customStack);
    }

    [Test]
    public async Task Cancel_CancelsCancellationToken()
    {
        await using var engine = new PLang.Runtime2.Engine.@this("/app");
        using var context = new PLangContext(engine);

        context.Cancel();

        await Assert.That(context.CancellationToken.IsCancellationRequested).IsTrue();
    }

    [Test]
    public async Task Duration_ReturnsPositiveTimeSpan()
    {
        await using var engine = new PLang.Runtime2.Engine.@this("/app");
        using var context = new PLangContext(engine);
        await Task.Delay(10);

        var duration = context.Duration;

        await Assert.That(duration.TotalMilliseconds).IsGreaterThan(0);
    }

    [Test]
    public async Task Dispose_CancelsToken()
    {
        await using var engine = new PLang.Runtime2.Engine.@this("/app");
        var context = new PLangContext(engine);
        var token = context.CancellationToken;

        context.Dispose();

        await Assert.That(token.IsCancellationRequested).IsTrue();
    }

    [Test]
    public async Task Dispose_ClearsData()
    {
        await using var engine = new PLang.Runtime2.Engine.@this("/app");
        var context = new PLangContext(engine);
        context["key"] = "value";

        context.Dispose();

        await Assert.That(context.ContainsKey("key")).IsFalse();
    }

    [Test]
    public async Task Dispose_DisposesDisposableValues()
    {
        await using var engine = new PLang.Runtime2.Engine.@this("/app");
        var context = new PLangContext(engine);
        var disposable = new TestDisposable();
        context["disposable"] = disposable;

        context.Dispose();

        await Assert.That(disposable.IsDisposed).IsTrue();
    }

    [Test]
    public async Task Dispose_CalledTwice_DoesNotThrow()
    {
        await using var engine = new PLang.Runtime2.Engine.@this("/app");
        var context = new PLangContext(engine);

        context.Dispose();
        context.Dispose();

        await Assert.That(context.ContainsKey("any")).IsFalse();
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

public class PLangContextAccessorTests
{
    [Test]
    public async Task Current_ReturnsNullInitially()
    {
        var accessor = new PLangContextAccessor();

        await Assert.That(accessor.Current).IsNull();
    }

    [Test]
    public async Task Current_SetAndGet_ReturnsSameContext()
    {
        var accessor = new PLangContextAccessor();
        await using var engine = new PLang.Runtime2.Engine.@this("/app");
        using var context = new PLangContext(engine);

        accessor.Current = context;

        await Assert.That(accessor.Current).IsEqualTo(context);
    }

    [Test]
    public async Task Current_SetNull_ReturnsNull()
    {
        var accessor = new PLangContextAccessor();
        await using var engine = new PLang.Runtime2.Engine.@this("/app");
        using var context = new PLangContext(engine);
        accessor.Current = context;

        accessor.Current = null;

        await Assert.That(accessor.Current).IsNull();
    }
}
