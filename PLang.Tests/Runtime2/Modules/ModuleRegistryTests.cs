using PLang.Runtime2.Core;
using PLang.Runtime2.Errors;
using PLang.Runtime2.Modules;

namespace PLang.Tests.Runtime2.Modules;

public class ModuleRegistryTests
{
    [Test]
    public async Task Constructor_StartsEmpty()
    {
        var registry = new ModuleRegistry();

        await Assert.That(registry.Count).IsEqualTo(0);
    }

    [Test]
    public async Task Register_AddsModule()
    {
        var registry = new ModuleRegistry();
        var module = CreateMockModule("test");

        registry.Register(module);

        await Assert.That(registry.Count).IsEqualTo(1);
    }

    [Test]
    public async Task Register_RegistersByName()
    {
        var registry = new ModuleRegistry();
        var module = CreateMockModule("test");

        registry.Register(module);

        await Assert.That(registry.Get("test")).IsEqualTo(module);
    }

    [Test]
    public async Task Register_RegistersAliases()
    {
        var registry = new ModuleRegistry();
        var module = CreateMockModule("variable", new[] { "var", "vars" });

        registry.Register(module);

        await Assert.That(registry.Get("var")).IsEqualTo(module);
        await Assert.That(registry.Get("vars")).IsEqualTo(module);
    }

    [Test]
    public async Task Register_Generic_CreatesAndRegistersModule()
    {
        var registry = new ModuleRegistry();

        registry.Register<TestModule>();

        await Assert.That(registry.Get("testmodule")).IsNotNull();
    }

    [Test]
    public async Task Get_ByName_ReturnsModule()
    {
        var registry = new ModuleRegistry();
        var module = CreateMockModule("test");
        registry.Register(module);

        var result = registry.Get("test");

        await Assert.That(result).IsEqualTo(module);
    }

    [Test]
    public async Task Get_ByAlias_ReturnsModule()
    {
        var registry = new ModuleRegistry();
        var module = CreateMockModule("test", new[] { "t", "tst" });
        registry.Register(module);

        await Assert.That(registry.Get("t")).IsEqualTo(module);
        await Assert.That(registry.Get("tst")).IsEqualTo(module);
    }

    [Test]
    public async Task Get_CaseInsensitive()
    {
        var registry = new ModuleRegistry();
        var module = CreateMockModule("Test");
        registry.Register(module);

        await Assert.That(registry.Get("test")).IsEqualTo(module);
        await Assert.That(registry.Get("TEST")).IsEqualTo(module);
    }

    [Test]
    public async Task Get_NullOrEmpty_ReturnsNull()
    {
        var registry = new ModuleRegistry();

        await Assert.That(registry.Get(null!)).IsNull();
        await Assert.That(registry.Get("")).IsNull();
    }

    [Test]
    public async Task Get_NonexistentName_ReturnsNull()
    {
        var registry = new ModuleRegistry();

        var result = registry.Get("nonexistent");

        await Assert.That(result).IsNull();
    }

    [Test]
    public async Task GetRequired_ExistingModule_ReturnsModule()
    {
        var registry = new ModuleRegistry();
        var module = CreateMockModule("test");
        registry.Register(module);

        var result = registry.GetRequired("test");

        await Assert.That(result).IsEqualTo(module);
    }

    [Test]
    public async Task GetRequired_NonexistentModule_ThrowsException()
    {
        var registry = new ModuleRegistry();

        await Assert.ThrowsAsync<ModuleNotFoundException>(async () =>
        {
            await Task.Run(() => registry.GetRequired("nonexistent"));
        });
    }

    [Test]
    public async Task Contains_ExistingModule_ReturnsTrue()
    {
        var registry = new ModuleRegistry();
        registry.Register(CreateMockModule("test"));

        await Assert.That(registry.Contains("test")).IsTrue();
    }

    [Test]
    public async Task Contains_ByAlias_ReturnsTrue()
    {
        var registry = new ModuleRegistry();
        registry.Register(CreateMockModule("test", new[] { "t" }));

        await Assert.That(registry.Contains("t")).IsTrue();
    }

    [Test]
    public async Task Contains_NonexistentModule_ReturnsFalse()
    {
        var registry = new ModuleRegistry();

        await Assert.That(registry.Contains("nonexistent")).IsFalse();
    }

    [Test]
    public async Task Remove_RemovesModule()
    {
        var registry = new ModuleRegistry();
        registry.Register(CreateMockModule("test", new[] { "t" }));

        var removed = registry.Remove("test");

        await Assert.That(removed).IsTrue();
        await Assert.That(registry.Count).IsEqualTo(0);
    }

    [Test]
    public async Task Remove_RemovesAliases()
    {
        var registry = new ModuleRegistry();
        registry.Register(CreateMockModule("test", new[] { "t" }));

        registry.Remove("test");

        await Assert.That(registry.Contains("t")).IsFalse();
    }

    [Test]
    public async Task Remove_NonexistentModule_ReturnsFalse()
    {
        var registry = new ModuleRegistry();

        var removed = registry.Remove("nonexistent");

        await Assert.That(removed).IsFalse();
    }

    [Test]
    public async Task Clear_RemovesAllModulesAndAliases()
    {
        var registry = new ModuleRegistry();
        registry.Register(CreateMockModule("module1", new[] { "m1" }));
        registry.Register(CreateMockModule("module2", new[] { "m2" }));

        registry.Clear();

        await Assert.That(registry.Count).IsEqualTo(0);
        await Assert.That(registry.Contains("m1")).IsFalse();
    }

    [Test]
    public async Task Names_ReturnsAllNames()
    {
        var registry = new ModuleRegistry();
        registry.Register(CreateMockModule("module1"));
        registry.Register(CreateMockModule("module2"));

        var names = registry.Names.ToList();

        await Assert.That(names).Contains("module1");
        await Assert.That(names).Contains("module2");
    }

    [Test]
    public async Task All_ReturnsAllModules()
    {
        var registry = new ModuleRegistry();
        var module1 = CreateMockModule("module1");
        var module2 = CreateMockModule("module2");
        registry.Register(module1);
        registry.Register(module2);

        var all = registry.All.ToList();

        await Assert.That(all).Contains(module1);
        await Assert.That(all).Contains(module2);
    }

    [Test]
    public async Task Aliases_ReturnsAllAliases()
    {
        var registry = new ModuleRegistry();
        registry.Register(CreateMockModule("module1", new[] { "m1", "mod1" }));
        registry.Register(CreateMockModule("module2", new[] { "m2" }));

        var aliases = registry.Aliases;

        await Assert.That(aliases.Count).IsEqualTo(3);
        await Assert.That(aliases["m1"]).IsEqualTo("module1");
        await Assert.That(aliases["mod1"]).IsEqualTo("module1");
        await Assert.That(aliases["m2"]).IsEqualTo("module2");
    }

    [Test]
    public async Task FindByMethod_ReturnsModulesThatCanHandle()
    {
        var registry = new ModuleRegistry();
        var module1 = CreateMockModule("module1", canHandle: "get");
        var module2 = CreateMockModule("module2", canHandle: "set");
        registry.Register(module1);
        registry.Register(module2);

        var result = registry.FindByMethod("get").ToList();

        await Assert.That(result.Count).IsEqualTo(1);
        await Assert.That(result[0]).IsEqualTo(module1);
    }

    [Test]
    public async Task FindByMethod_NoMatches_ReturnsEmpty()
    {
        var registry = new ModuleRegistry();
        registry.Register(CreateMockModule("module1", canHandle: "get"));

        var result = registry.FindByMethod("post").ToList();

        await Assert.That(result.Count).IsEqualTo(0);
    }

    [Test]
    public async Task Register_SameNameTwice_ReplacesModule()
    {
        var registry = new ModuleRegistry();
        var module1 = CreateMockModule("test");
        var module2 = CreateMockModule("test");
        registry.Register(module1);

        registry.Register(module2);

        await Assert.That(registry.Get("test")).IsEqualTo(module2);
    }

    private static IModule CreateMockModule(string name, string[]? aliases = null, string? canHandle = null)
    {
        return new MockModule(name, aliases ?? Array.Empty<string>(), canHandle);
    }

    private class MockModule : IModule
    {
        private readonly string _canHandle;
        public string Name { get; }
        public IEnumerable<string> Aliases { get; }

        public MockModule(string name, IEnumerable<string> aliases, string? canHandle = null)
        {
            Name = name;
            Aliases = aliases;
            _canHandle = canHandle ?? "";
        }

        public void Initialize(ModuleContext context) { }
        public Task<GoalResult> ExecuteAsync(string method, object? parameters) => GoalResult.SuccessTask();
        public bool CanHandle(string method) => !string.IsNullOrEmpty(_canHandle) && _canHandle == method;
        public IEnumerable<string> GetMethods() => Array.Empty<string>();
    }

    private class TestModule : IModule
    {
        public string Name => "testmodule";
        public IEnumerable<string> Aliases => Array.Empty<string>();
        public void Initialize(ModuleContext context) { }
        public Task<GoalResult> ExecuteAsync(string method, object? parameters) => GoalResult.SuccessTask();
        public bool CanHandle(string method) => false;
        public IEnumerable<string> GetMethods() => Array.Empty<string>();
    }
}
