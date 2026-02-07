using PLang.Runtime2.Context;
using PLang.Runtime2.Core;
using PLang.Runtime2.Modules;

namespace PLang.Tests.Runtime2.Modules;

public class ActionRegistryTests
{
    [Test]
    public async Task Constructor_StartsEmpty()
    {
        var registry = new ActionRegistry();

        await Assert.That(registry.Count).IsEqualTo(0);
    }

    [Test]
    public async Task Register_AddsHandler()
    {
        var registry = new ActionRegistry();
        var handler = new MockHandler();

        registry.Register("test", "do", handler);

        await Assert.That(registry.Count).IsEqualTo(1);
    }

    [Test]
    public async Task Get_ReturnsHandler()
    {
        var registry = new ActionRegistry();
        var handler = new MockHandler();
        registry.Register("test", "do", handler);

        var result = registry.Get("test", "do");

        await Assert.That(result).IsEqualTo(handler);
    }

    [Test]
    public async Task Get_CaseInsensitive()
    {
        var registry = new ActionRegistry();
        var handler = new MockHandler();
        registry.Register("Test", "Do", handler);

        await Assert.That(registry.Get("test", "do")).IsEqualTo(handler);
        await Assert.That(registry.Get("TEST", "DO")).IsEqualTo(handler);
    }

    [Test]
    public async Task Get_NullOrEmpty_ReturnsNull()
    {
        var registry = new ActionRegistry();

        await Assert.That(registry.Get(null!, "do")).IsNull();
        await Assert.That(registry.Get("", "do")).IsNull();
        await Assert.That(registry.Get("test", null!)).IsNull();
        await Assert.That(registry.Get("test", "")).IsNull();
    }

    [Test]
    public async Task Get_NonexistentHandler_ReturnsNull()
    {
        var registry = new ActionRegistry();

        await Assert.That(registry.Get("nonexistent", "method")).IsNull();
    }

    [Test]
    public async Task Contains_WithNamespaceAndClass_ReturnsTrue()
    {
        var registry = new ActionRegistry();
        registry.Register("test", "do", new MockHandler());

        await Assert.That(registry.Contains("test", "do")).IsTrue();
    }

    [Test]
    public async Task Contains_WithNamespaceOnly_ReturnsTrue()
    {
        var registry = new ActionRegistry();
        registry.Register("test", "do", new MockHandler());

        await Assert.That(registry.Contains("test")).IsTrue();
    }

    [Test]
    public async Task Contains_NonexistentNamespace_ReturnsFalse()
    {
        var registry = new ActionRegistry();

        await Assert.That(registry.Contains("nonexistent")).IsFalse();
    }

    [Test]
    public async Task GetClasses_ReturnsAllClassesInNamespace()
    {
        var registry = new ActionRegistry();
        registry.Register("variable", "set", new MockHandler());
        registry.Register("variable", "get", new MockHandler());

        var classes = registry.GetClasses("variable").ToList();

        await Assert.That(classes).Contains("set");
        await Assert.That(classes).Contains("get");
    }

    [Test]
    public async Task GetClasses_NonexistentNamespace_ReturnsEmpty()
    {
        var registry = new ActionRegistry();

        var classes = registry.GetClasses("nonexistent").ToList();

        await Assert.That(classes.Count).IsEqualTo(0);
    }

    [Test]
    public async Task Namespaces_ReturnsAllNamespaces()
    {
        var registry = new ActionRegistry();
        registry.Register("variable", "set", new MockHandler());
        registry.Register("output", "write", new MockHandler());

        var namespaces = registry.Namespaces.ToList();

        await Assert.That(namespaces).Contains("variable");
        await Assert.That(namespaces).Contains("output");
    }

    [Test]
    public async Task Clear_RemovesAllHandlers()
    {
        var registry = new ActionRegistry();
        registry.Register("variable", "set", new MockHandler());

        registry.Clear();

        await Assert.That(registry.Count).IsEqualTo(0);
        await Assert.That(registry.Contains("variable")).IsFalse();
    }

    [Test]
    public async Task Register_SameKeyTwice_ReplacesHandler()
    {
        var registry = new ActionRegistry();
        var handler1 = new MockHandler();
        var handler2 = new MockHandler();
        registry.Register("test", "do", handler1);

        registry.Register("test", "do", handler2);

        await Assert.That(registry.Get("test", "do")).IsEqualTo(handler2);
    }

    [Test]
    public async Task DiscoverAndRegister_FindsIClassImplementations()
    {
        var registry = new ActionRegistry();

        registry.DiscoverAndRegister(typeof(Engine).Assembly);

        // Should discover variable.set, variable.get, etc.
        await Assert.That(registry.Contains("variable", "set")).IsTrue();
        await Assert.That(registry.Contains("variable", "get")).IsTrue();
        await Assert.That(registry.Contains("variable", "remove")).IsTrue();
        await Assert.That(registry.Contains("variable", "exists")).IsTrue();
        await Assert.That(registry.Contains("variable", "clear")).IsTrue();
        await Assert.That(registry.Contains("output", "write")).IsTrue();
        await Assert.That(registry.Contains("file", "save")).IsTrue();
        await Assert.That(registry.Contains("file", "read")).IsTrue();
        await Assert.That(registry.Contains("file", "delete")).IsTrue();
        await Assert.That(registry.Contains("file", "exists")).IsTrue();
        await Assert.That(registry.Contains("file", "copy")).IsTrue();
        await Assert.That(registry.Contains("file", "move")).IsTrue();
    }

    [Test]
    public async Task All_ReturnsAllHandlers()
    {
        var registry = new ActionRegistry();
        var handler1 = new MockHandler();
        var handler2 = new MockHandler();
        registry.Register("ns1", "cls1", handler1);
        registry.Register("ns2", "cls2", handler2);

        var all = registry.All.ToList();

        await Assert.That(all).Contains(handler1);
        await Assert.That(all).Contains(handler2);
    }

    private class MockHandler : IClass
    {
        public Engine Engine { get; private set; } = null!;
        public PLangContext Context { get; private set; } = null!;
        public Type? ParameterType => null;
        public void Initialize(Engine engine, PLangContext context) { Engine = engine; Context = context; }
        public Task<Return> ExecuteAsync(object? parameters) => Task.FromResult(new Return());
    }
}
