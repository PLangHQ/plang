using PLang.Runtime2.Context;
using PLang.Runtime2.Memory;

namespace PLang.Tests.Runtime2.Context;

public class PLangContextTests
{
    private PLangAppContext CreateAppContext() => new PLangAppContext("/app");

    [Test]
    public async Task Constructor_SetsProperties()
    {
        using var appContext = CreateAppContext();
        using var context = new PLangContext(appContext);

        await Assert.That(context.AppContext).IsEqualTo(appContext);
        await Assert.That(context.MemoryStack).IsNotNull();
        await Assert.That(context.Parent).IsNull();
        await Assert.That(context.Depth).IsEqualTo(0);
    }

    [Test]
    public async Task Constructor_GeneratesId()
    {
        using var appContext = CreateAppContext();
        using var context = new PLangContext(appContext);

        await Assert.That(context.Id).IsNotNull();
        await Assert.That(context.Id.Length).IsEqualTo(12);
    }

    [Test]
    public async Task Constructor_SetsCreatedAt()
    {
        using var appContext = CreateAppContext();
        var before = DateTime.UtcNow;

        using var context = new PLangContext(appContext);

        var after = DateTime.UtcNow;
        await Assert.That(context.CreatedAt).IsGreaterThanOrEqualTo(before);
        await Assert.That(context.CreatedAt).IsLessThanOrEqualTo(after);
    }

    [Test]
    public async Task Constructor_AcceptsCustomMemoryStack()
    {
        using var appContext = CreateAppContext();
        var memoryStack = new MemoryStack();
        memoryStack.Set("test", "value");

        using var context = new PLangContext(appContext, memoryStack);

        await Assert.That(context.MemoryStack).IsEqualTo(memoryStack);
    }

    [Test]
    public async Task Constructor_WithParent_SetsParentAndDepth()
    {
        using var appContext = CreateAppContext();
        using var parent = new PLangContext(appContext);

        using var child = new PLangContext(appContext, parent: parent);

        await Assert.That(child.Parent).IsEqualTo(parent);
        await Assert.That(child.Depth).IsEqualTo(1);
    }

    [Test]
    public async Task CallStack_DefaultsToNull()
    {
        using var appContext = CreateAppContext();
        using var context = new PLangContext(appContext);

        await Assert.That(context.CallStack).IsNull();
    }

    [Test]
    public async Task CallStack_CanBeSet()
    {
        using var appContext = CreateAppContext();
        using var context = new PLangContext(appContext);
        var callStack = new PLang.Runtime2.Core.CallStack();

        context.CallStack = callStack;

        await Assert.That(context.CallStack).IsEqualTo(callStack);
    }

    [Test]
    public async Task IsAsync_DefaultsFalse()
    {
        using var appContext = CreateAppContext();
        using var context = new PLangContext(appContext);

        await Assert.That(context.IsAsync).IsFalse();
    }

    [Test]
    public async Task IsAsync_CanBeSet()
    {
        using var appContext = CreateAppContext();
        using var context = new PLangContext(appContext);

        context.IsAsync = true;

        await Assert.That(context.IsAsync).IsTrue();
    }

    [Test]
    public async Task CurrentGoalName_DefaultsToNull()
    {
        using var appContext = CreateAppContext();
        using var context = new PLangContext(appContext);

        await Assert.That(context.CurrentGoalName).IsNull();
    }

    [Test]
    public async Task CurrentGoalName_CanBeSet()
    {
        using var appContext = CreateAppContext();
        using var context = new PLangContext(appContext);

        context.CurrentGoalName = "TestGoal";

        await Assert.That(context.CurrentGoalName).IsEqualTo("TestGoal");
    }

    [Test]
    public async Task CurrentStepIndex_DefaultsToNull()
    {
        using var appContext = CreateAppContext();
        using var context = new PLangContext(appContext);

        await Assert.That(context.CurrentStepIndex).IsNull();
    }

    [Test]
    public async Task CurrentStepIndex_CanBeSet()
    {
        using var appContext = CreateAppContext();
        using var context = new PLangContext(appContext);

        context.CurrentStepIndex = 5;

        await Assert.That(context.CurrentStepIndex).IsEqualTo(5);
    }

    [Test]
    public async Task CancellationToken_LinkedToAppShutdown()
    {
        using var appContext = CreateAppContext();
        using var context = new PLangContext(appContext);

        appContext.RequestShutdown();

        await Assert.That(context.CancellationToken.IsCancellationRequested).IsTrue();
    }

    [Test]
    public async Task Indexer_SetsAndGetsValue()
    {
        using var appContext = CreateAppContext();
        using var context = new PLangContext(appContext);

        context["key"] = "value";

        await Assert.That(context["key"]).IsEqualTo("value");
    }

    [Test]
    public async Task Indexer_SetNull_RemovesKey()
    {
        using var appContext = CreateAppContext();
        using var context = new PLangContext(appContext);
        context["key"] = "value";

        context["key"] = null;

        await Assert.That(context["key"]).IsNull();
    }

    [Test]
    public async Task Indexer_CaseInsensitive()
    {
        using var appContext = CreateAppContext();
        using var context = new PLangContext(appContext);
        context["Key"] = "value";

        await Assert.That(context["key"]).IsEqualTo("value");
        await Assert.That(context["KEY"]).IsEqualTo("value");
    }

    [Test]
    public async Task Get_ReturnsTypedValue()
    {
        using var appContext = CreateAppContext();
        using var context = new PLangContext(appContext);
        context["count"] = 42;

        var value = context.Get<int>("count");

        await Assert.That(value).IsEqualTo(42);
    }

    [Test]
    public async Task Get_NonexistentKey_ReturnsDefault()
    {
        using var appContext = CreateAppContext();
        using var context = new PLangContext(appContext);

        var value = context.Get<int>("nonexistent");

        await Assert.That(value).IsEqualTo(0);
    }

    [Test]
    public async Task Set_StoresTypedValue()
    {
        using var appContext = CreateAppContext();
        using var context = new PLangContext(appContext);

        context.Set("count", 42);

        await Assert.That(context.Get<int>("count")).IsEqualTo(42);
    }

    [Test]
    public async Task Set_Null_RemovesKey()
    {
        using var appContext = CreateAppContext();
        using var context = new PLangContext(appContext);
        context.Set<string>("key", "value");

        context.Set<string>("key", null!);

        await Assert.That(context["key"]).IsNull();
    }

    [Test]
    public async Task ContainsKey_ExistingKey_ReturnsTrue()
    {
        using var appContext = CreateAppContext();
        using var context = new PLangContext(appContext);
        context["key"] = "value";

        await Assert.That(context.ContainsKey("key")).IsTrue();
    }

    [Test]
    public async Task ContainsKey_NonexistentKey_ReturnsFalse()
    {
        using var appContext = CreateAppContext();
        using var context = new PLangContext(appContext);

        await Assert.That(context.ContainsKey("nonexistent")).IsFalse();
    }

    [Test]
    public async Task CreateChild_CreatesWithClonedMemoryStack()
    {
        using var appContext = CreateAppContext();
        using var parent = new PLangContext(appContext);
        parent.MemoryStack.Set("test", "value");

        using var child = parent.CreateChild();

        await Assert.That(child.MemoryStack.GetValue("test")).IsEqualTo("value");
        await Assert.That(child.MemoryStack).IsNotEqualTo(parent.MemoryStack);
    }

    [Test]
    public async Task CreateChild_SetsParent()
    {
        using var appContext = CreateAppContext();
        using var parent = new PLangContext(appContext);

        using var child = parent.CreateChild();

        await Assert.That(child.Parent).IsEqualTo(parent);
    }

    [Test]
    public async Task CreateChild_IncrementsDepth()
    {
        using var appContext = CreateAppContext();
        using var parent = new PLangContext(appContext);

        using var child = parent.CreateChild();

        await Assert.That(child.Depth).IsEqualTo(1);
    }

    [Test]
    public async Task CreateChild_AcceptsCustomMemoryStack()
    {
        using var appContext = CreateAppContext();
        using var parent = new PLangContext(appContext);
        var customStack = new MemoryStack();
        customStack.Set("custom", "value");

        using var child = parent.CreateChild(customStack);

        await Assert.That(child.MemoryStack).IsEqualTo(customStack);
    }

    [Test]
    public async Task Clone_CreatesIndependentCopy()
    {
        using var appContext = CreateAppContext();
        using var original = new PLangContext(appContext);
        original["key"] = "value";
        original.IsAsync = true;
        original.CurrentGoalName = "TestGoal";

        using var clone = original.Clone();

        await Assert.That(clone.Get<string>("key")).IsEqualTo("value");
        await Assert.That(clone.IsAsync).IsTrue();
        await Assert.That(clone.CurrentGoalName).IsEqualTo("TestGoal");
    }

    [Test]
    public async Task Clone_IndependentData()
    {
        using var appContext = CreateAppContext();
        using var original = new PLangContext(appContext);
        original["key"] = "value";

        using var clone = original.Clone();
        clone["key"] = "modified";

        await Assert.That(original.Get<string>("key")).IsEqualTo("value");
        await Assert.That(clone.Get<string>("key")).IsEqualTo("modified");
    }

    [Test]
    public async Task Clone_AcceptsCustomMemoryStack()
    {
        using var appContext = CreateAppContext();
        using var original = new PLangContext(appContext);
        var customStack = new MemoryStack();
        customStack.Set("custom", "value");

        using var clone = original.Clone(customStack);

        await Assert.That(clone.MemoryStack).IsEqualTo(customStack);
    }

    [Test]
    public async Task Cancel_CancelsCancellationToken()
    {
        using var appContext = CreateAppContext();
        using var context = new PLangContext(appContext);

        context.Cancel();

        await Assert.That(context.CancellationToken.IsCancellationRequested).IsTrue();
    }

    [Test]
    public async Task Duration_ReturnsPositiveTimeSpan()
    {
        using var appContext = CreateAppContext();
        using var context = new PLangContext(appContext);
        await Task.Delay(10);

        var duration = context.Duration;

        await Assert.That(duration.TotalMilliseconds).IsGreaterThan(0);
    }

    [Test]
    public async Task Dispose_CancelsToken()
    {
        using var appContext = CreateAppContext();
        var context = new PLangContext(appContext);
        var token = context.CancellationToken;

        context.Dispose();

        await Assert.That(token.IsCancellationRequested).IsTrue();
    }

    [Test]
    public async Task Dispose_ClearsData()
    {
        using var appContext = CreateAppContext();
        var context = new PLangContext(appContext);
        context["key"] = "value";

        context.Dispose();

        await Assert.That(context.ContainsKey("key")).IsFalse();
    }

    [Test]
    public async Task Dispose_DisposesDisposableValues()
    {
        using var appContext = CreateAppContext();
        var context = new PLangContext(appContext);
        var disposable = new TestDisposable();
        context["disposable"] = disposable;

        context.Dispose();

        await Assert.That(disposable.IsDisposed).IsTrue();
    }

    [Test]
    public async Task Dispose_CalledTwice_DoesNotThrow()
    {
        using var appContext = CreateAppContext();
        var context = new PLangContext(appContext);

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
        using var appContext = new PLangAppContext("/app");
        using var context = new PLangContext(appContext);

        accessor.Current = context;

        await Assert.That(accessor.Current).IsEqualTo(context);
    }

    [Test]
    public async Task Current_SetNull_ReturnsNull()
    {
        var accessor = new PLangContextAccessor();
        using var appContext = new PLangAppContext("/app");
        using var context = new PLangContext(appContext);
        accessor.Current = context;

        accessor.Current = null;

        await Assert.That(accessor.Current).IsNull();
    }
}
