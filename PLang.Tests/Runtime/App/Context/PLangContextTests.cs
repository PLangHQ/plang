using app.actor.context;
using app;
using app.variable;

namespace PLang.Tests.App.Context;

public class PLangContextTests
{
    [Test]
    public async Task Constructor_SetsProperties()
    {
        await using var engine = TestApp.Create("/app");
        using var context = new global::app.actor.context.@this(engine, engine.User);

        await Assert.That(context.App).IsEqualTo(engine);
        await Assert.That(context.Variable).IsNotNull();
        await Assert.That(context.Parent).IsNull();
    }

    [Test]
    public async Task Constructor_GeneratesId()
    {
        await using var engine = TestApp.Create("/app");
        using var context = new global::app.actor.context.@this(engine, engine.User);

        await Assert.That(context.Id).IsNotNull();
        await Assert.That(context.Id.Length).IsEqualTo(12);
    }

    [Test]
    public async Task Constructor_SetsCreatedAt()
    {
        await using var engine = TestApp.Create("/app");
        var before = DateTime.UtcNow;

        using var context = new global::app.actor.context.@this(engine, engine.User);

        var after = DateTime.UtcNow;
        await Assert.That(context.CreatedAt).IsGreaterThanOrEqualTo(before);
        await Assert.That(context.CreatedAt).IsLessThanOrEqualTo(after);
    }

    [Test]
    public async Task Constructor_AcceptsCustomVariables()
    {
        await using var engine = TestApp.Create("/app");
        var variables = new Variables(engine.User.Context);
        variables.Set("test", "value");

        using var context = new global::app.actor.context.@this(engine, engine.User, variables);

        await Assert.That(context.Variable).IsEqualTo(variables);
    }

    [Test]
    public async Task Constructor_WithParent_SetsParent()
    {
        await using var engine = TestApp.Create("/app");
        using var parent = new global::app.actor.context.@this(engine, engine.User);

        using var child = new global::app.actor.context.@this(engine, engine.User, parent: parent);

        await Assert.That(child.Parent).IsEqualTo(parent);
    }

    [Test]
    public async Task CallStack_ReadsThrough_ActorCallStack()
    {
        // Each actor owns its call tree. The Context exposes a getter that proxies through
        // to its owning Actor's CallStack so PLang %!callStack% still resolves; there's no
        // per-context allocation.
        await using var engine = TestApp.Create("/app");
        using var context = new global::app.actor.context.@this(engine, engine.User);

        await Assert.That(context.CallStack).IsEqualTo(engine.User.CallStack);
    }

    [Test]
    public async Task IsAsync_DefaultsFalse()
    {
        await using var engine = TestApp.Create("/app");
        using var context = new global::app.actor.context.@this(engine, engine.User);

        await Assert.That(context.IsAsync).IsFalse();
    }

    [Test]
    public async Task IsAsync_CanBeSet()
    {
        await using var engine = TestApp.Create("/app");
        using var context = new global::app.actor.context.@this(engine, engine.User);

        context.IsAsync = true;

        await Assert.That(context.IsAsync).IsTrue();
    }

    [Test]
    public async Task CancellationToken_LinkedToAppShutdown()
    {
        await using var engine = TestApp.Create("/app");
        using var context = new global::app.actor.context.@this(engine, engine.User);

        engine.RequestShutdown();

        await Assert.That(context.CancellationToken.IsCancellationRequested).IsTrue();
    }

    [Test]
    public async Task Indexer_SetsAndGetsValue()
    {
        await using var engine = TestApp.Create("/app");
        using var context = new global::app.actor.context.@this(engine, engine.User);

        context["key"] = "value";

        await Assert.That(context["key"]).IsEqualTo("value");
    }

    [Test]
    public async Task Indexer_SetNull_RemovesKey()
    {
        await using var engine = TestApp.Create("/app");
        using var context = new global::app.actor.context.@this(engine, engine.User);
        context["key"] = "value";

        context["key"] = null;

        await Assert.That(context["key"]).IsNull();
    }

    [Test]
    public async Task Indexer_CaseInsensitive()
    {
        await using var engine = TestApp.Create("/app");
        using var context = new global::app.actor.context.@this(engine, engine.User);
        context["Key"] = "value";

        await Assert.That(context["key"]).IsEqualTo("value");
        await Assert.That(context["KEY"]).IsEqualTo("value");
    }

    [Test]
    public async Task Get_ReturnsTypedValue()
    {
        await using var engine = TestApp.Create("/app");
        using var context = new global::app.actor.context.@this(engine, engine.User);
        context["count"] = 42;

        var value = context.Get<int>("count");

        await Assert.That(value).IsEqualTo(42);
    }

    [Test]
    public async Task Get_NonexistentKey_ReturnsDefault()
    {
        await using var engine = TestApp.Create("/app");
        using var context = new global::app.actor.context.@this(engine, engine.User);

        var value = context.Get<int>("nonexistent");

        await Assert.That(value).IsEqualTo(0);
    }

    [Test]
    public async Task Set_StoresTypedValue()
    {
        await using var engine = TestApp.Create("/app");
        using var context = new global::app.actor.context.@this(engine, engine.User);

        context.Set("count", 42);

        await Assert.That(context.Get<int>("count")).IsEqualTo(42);
    }

    [Test]
    public async Task Set_Null_RemovesKey()
    {
        await using var engine = TestApp.Create("/app");
        using var context = new global::app.actor.context.@this(engine, engine.User);
        context.Set<string>("key", "value");

        context.Set<string>("key", null!);

        await Assert.That(context["key"]).IsNull();
    }

    [Test]
    public async Task ContainsKey_ExistingKey_ReturnsTrue()
    {
        await using var engine = TestApp.Create("/app");
        using var context = new global::app.actor.context.@this(engine, engine.User);
        context["key"] = "value";

        await Assert.That(context.ContainsKey("key")).IsTrue();
    }

    [Test]
    public async Task ContainsKey_NonexistentKey_ReturnsFalse()
    {
        await using var engine = TestApp.Create("/app");
        using var context = new global::app.actor.context.@this(engine, engine.User);

        await Assert.That(context.ContainsKey("nonexistent")).IsFalse();
    }

    [Test]
    public async Task CreateChild_CreatesWithClonedVariables()
    {
        await using var engine = TestApp.Create("/app");
        using var parent = new global::app.actor.context.@this(engine, engine.User);
        parent.Variable.Set("test", "value");

        using var child = parent.CreateChild();

        await Assert.That((await child.Variable.GetValue("test"))).IsEqualTo("value");
        await Assert.That(child.Variable).IsNotEqualTo(parent.Variable);
    }

    [Test]
    public async Task CreateChild_SetsParent()
    {
        await using var engine = TestApp.Create("/app");
        using var parent = new global::app.actor.context.@this(engine, engine.User);

        using var child = parent.CreateChild();

        await Assert.That(child.Parent).IsEqualTo(parent);
    }

    [Test]
    public async Task CreateChild_AcceptsCustomVariables()
    {
        await using var engine = TestApp.Create("/app");
        using var parent = new global::app.actor.context.@this(engine, engine.User);
        var customStack = new Variables(engine.User.Context);
        customStack.Set("custom", "value");

        using var child = parent.CreateChild(customStack);

        await Assert.That(child.Variable).IsEqualTo(customStack);
    }

    [Test]
    public async Task Clone_CreatesIndependentCopy()
    {
        await using var engine = TestApp.Create("/app");
        using var original = new global::app.actor.context.@this(engine, engine.User);
        original["key"] = "value";
        original.IsAsync = true;

        using var clone = original.Clone();

        await Assert.That(clone.Get<string>("key")).IsEqualTo("value");
        await Assert.That(clone.IsAsync).IsTrue();
    }

    [Test]
    public async Task Clone_IndependentData()
    {
        await using var engine = TestApp.Create("/app");
        using var original = new global::app.actor.context.@this(engine, engine.User);
        original["key"] = "value";

        using var clone = original.Clone();
        clone["key"] = "modified";

        await Assert.That(original.Get<string>("key")).IsEqualTo("value");
        await Assert.That(clone.Get<string>("key")).IsEqualTo("modified");
    }

    [Test]
    public async Task Clone_AcceptsCustomVariables()
    {
        await using var engine = TestApp.Create("/app");
        using var original = new global::app.actor.context.@this(engine, engine.User);
        var customStack = new Variables(engine.User.Context);
        customStack.Set("custom", "value");

        using var clone = original.Clone(customStack);

        await Assert.That(clone.Variable).IsEqualTo(customStack);
    }

    [Test]
    public async Task Cancel_CancelsCancellationToken()
    {
        await using var engine = TestApp.Create("/app");
        using var context = new global::app.actor.context.@this(engine, engine.User);

        context.Cancel();

        await Assert.That(context.CancellationToken.IsCancellationRequested).IsTrue();
    }

    [Test]
    public async Task Duration_ReturnsPositiveTimeSpan()
    {
        await using var engine = TestApp.Create("/app");
        using var context = new global::app.actor.context.@this(engine, engine.User);
        await Task.Delay(10);

        var duration = context.Duration;

        await Assert.That(duration.TotalMilliseconds).IsGreaterThan(0);
    }

    [Test]
    public async Task Dispose_CancelsToken()
    {
        await using var engine = TestApp.Create("/app");
        var context = new global::app.actor.context.@this(engine, engine.User);
        var token = context.CancellationToken;

        context.Dispose();

        await Assert.That(token.IsCancellationRequested).IsTrue();
    }

    [Test]
    public async Task Dispose_ClearsData()
    {
        await using var engine = TestApp.Create("/app");
        var context = new global::app.actor.context.@this(engine, engine.User);
        context["key"] = "value";

        context.Dispose();

        await Assert.That(context.ContainsKey("key")).IsFalse();
    }

    [Test]
    public async Task Dispose_DisposesDisposableValues()
    {
        await using var engine = TestApp.Create("/app");
        var context = new global::app.actor.context.@this(engine, engine.User);
        var disposable = new TestDisposable();
        context["disposable"] = disposable;

        context.Dispose();

        await Assert.That(disposable.IsDisposed).IsTrue();
    }

    [Test]
    public async Task Dispose_CalledTwice_DoesNotThrow()
    {
        await using var engine = TestApp.Create("/app");
        var context = new global::app.actor.context.@this(engine, engine.User);

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
        var accessor = new global::app.actor.context.@thisAccessor();

        await Assert.That(accessor.Current).IsNull();
    }

    [Test]
    public async Task Current_SetAndGet_ReturnsSameContext()
    {
        var accessor = new global::app.actor.context.@thisAccessor();
        await using var engine = TestApp.Create("/app");
        using var context = new global::app.actor.context.@this(engine, engine.User);

        accessor.Current = context;

        await Assert.That(accessor.Current).IsEqualTo(context);
    }

    [Test]
    public async Task Current_SetNull_ReturnsNull()
    {
        var accessor = new global::app.actor.context.@thisAccessor();
        await using var engine = TestApp.Create("/app");
        using var context = new global::app.actor.context.@this(engine, engine.User);
        accessor.Current = context;

        accessor.Current = null;

        await Assert.That(accessor.Current).IsNull();
    }
}
