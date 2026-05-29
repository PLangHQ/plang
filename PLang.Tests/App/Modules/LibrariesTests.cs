using app.actor.context;
using app.variable;
using app.modules;

namespace PLang.Tests.App.actions;

public class LibrariesTests
{
    [Test]
    public async Task Constructor_DiscoversBultInHandlers()
    {
        var modules = new EngineModules();

        // EngineModules constructor auto-discovers built-in handlers
        await Assert.That(modules.Contains("variable", "set")).IsTrue();
        await Assert.That(modules.Contains("output", "write")).IsTrue();
    }

    [Test]
    public async Task Register_AddsHandler()
    {
        var modules = new EngineModules();
        var handler = new MockHandler();

        modules.Register("test", "do", handler);

        await Assert.That(modules.Contains("test", "do")).IsTrue();
    }

    [Test]
    public async Task Register_CaseInsensitive()
    {
        var modules = new EngineModules();
        var handler = new MockHandler();
        modules.Register("Test", "Do", handler);

        await Assert.That(modules.Contains("test", "do")).IsTrue();
        await Assert.That(modules.Contains("TEST", "DO")).IsTrue();
    }

    [Test]
    public async Task Contains_WithModuleAndAction_ReturnsTrue()
    {
        var modules = new EngineModules();
        modules.Register("test", "do", new MockHandler());

        await Assert.That(modules.Contains("test", "do")).IsTrue();
    }

    [Test]
    public async Task Contains_WithModuleOnly_ReturnsTrue()
    {
        var modules = new EngineModules();
        modules.Register("test", "do", new MockHandler());

        await Assert.That(modules.Contains("test")).IsTrue();
    }

    [Test]
    public async Task Contains_NonexistentModule_ReturnsFalse()
    {
        var modules = new EngineModules();

        await Assert.That(modules.Contains("nonexistent_xyz_123")).IsFalse();
    }

    [Test]
    public async Task GetActions_ReturnsAllActionsInModule()
    {
        var modules = new EngineModules();
        modules.Register("custom", "alpha", new MockHandler());
        modules.Register("custom", "beta", new MockHandler());

        var actions = modules.GetActions("custom").ToList();

        await Assert.That(actions).Contains("alpha");
        await Assert.That(actions).Contains("beta");
    }

    [Test]
    public async Task GetActions_NonexistentModule_ReturnsEmpty()
    {
        var modules = new EngineModules();

        var actions = modules.GetActions("nonexistent_xyz_123").ToList();

        await Assert.That(actions.Count).IsEqualTo(0);
    }

    [Test]
    public async Task Names_ReturnsAllModules()
    {
        var modules = new EngineModules();

        var names = modules.Names.ToList();

        // Built-in modules should be present
        await Assert.That(names).Contains("variable");
        await Assert.That(names).Contains("output");
    }

    [Test]
    public async Task Clear_RemovesAllHandlers()
    {
        var modules = new EngineModules();
        modules.Register("custom", "do", new MockHandler());

        modules.Clear();

        await Assert.That(modules.Contains("custom")).IsFalse();
        // Built-in handlers also cleared
        await Assert.That(modules.Contains("variable")).IsFalse();
    }

    [Test]
    public async Task Register_SameKeyTwice_ReplacesHandler()
    {
        var modules = new EngineModules();
        var handler1 = new MockHandler();
        var handler2 = new MockHandler();
        modules.Register("test", "do", handler1);

        modules.Register("test", "do", handler2);

        // GetCodeGenerated won't work for MockHandler (not ICodeGenerated),
        // but GetActionType confirms the replacement
        await Assert.That(modules.GetActionType("test", "do")).IsEqualTo(typeof(MockHandler));
    }

    [Test]
    public async Task BuiltIn_DiscoversFindHandlers()
    {
        var modules = new EngineModules();

        // Should discover variable.set, variable.get, etc.
        await Assert.That(modules.Contains("variable", "set")).IsTrue();
        await Assert.That(modules.Contains("variable", "get")).IsTrue();
        await Assert.That(modules.Contains("variable", "remove")).IsTrue();
        await Assert.That(modules.Contains("variable", "exists")).IsTrue();
        await Assert.That(modules.Contains("variable", "clear")).IsTrue();
        await Assert.That(modules.Contains("output", "write")).IsTrue();
        await Assert.That(modules.Contains("file", "save")).IsTrue();
        await Assert.That(modules.Contains("file", "read")).IsTrue();
        await Assert.That(modules.Contains("file", "delete")).IsTrue();
        await Assert.That(modules.Contains("file", "exists")).IsTrue();
        await Assert.That(modules.Contains("file", "copy")).IsTrue();
        await Assert.That(modules.Contains("file", "move")).IsTrue();
    }

    [Test]
    public async Task All_ReturnsRegisteredHandlers()
    {
        var modules = new EngineModules();
        var handler1 = new MockHandler();
        var handler2 = new MockHandler();
        modules.Register("ns1", "cls1", handler1);
        modules.Register("ns2", "cls2", handler2);

        var all = modules.All.ToList();

        await Assert.That(all).Contains(handler1);
        await Assert.That(all).Contains(handler2);
    }

    [Test]
    public async Task Register_DirectlyOnModules()
    {
        var modules = new EngineModules();
        modules.Register("custom", "magic", new MockHandler());

        await Assert.That(modules.Contains("custom", "magic")).IsTrue();
    }

    #region EngineModules.GetCodeGenerated

    [Test]
    public async Task GetCodeGenerated_BuiltInAction_ReturnsAction()
    {
        var modules = new EngineModules();
        await using var engine = new global::app.@this("/app", modules);
        var context = engine.User.Context;

        var (action, error) = modules.GetCodeGenerated(new PrAction { Module = "variable", ActionName = "set" });

        await Assert.That(action).IsNotNull();
        await Assert.That(error).IsNull();
    }

    [Test]
    public async Task GetCodeGenerated_ExplicitCodeGenAction_ReturnsAction()
    {
        var modules = new EngineModules();
        var action = new MockCodeGenHandler();
        modules.Register("custom", "run", action);
        await using var engine = new global::app.@this("/app", modules);
        var context = engine.User.Context;

        var (result, error) = modules.GetCodeGenerated(new PrAction { Module = "custom", ActionName = "run" });

        await Assert.That(result).IsEqualTo(action);
        await Assert.That(error).IsNull();
    }

    [Test]
    public async Task GetCodeGenerated_NonICodeGeneratedAction_ReturnsActionError()
    {
        var modules = new EngineModules();
        modules.Register("legacy", "do", new MockHandler());
        await using var engine = new global::app.@this("/app", modules);
        var context = engine.User.Context;

        var (action, error) = modules.GetCodeGenerated(new PrAction { Module = "legacy", ActionName = "do" });

        await Assert.That(action).IsNull();
        await Assert.That(error).IsNotNull();
        await Assert.That(error!.Key).IsEqualTo("ActionError");
    }

    [Test]
    public async Task GetCodeGenerated_NotFound_ReturnsActionNotFound()
    {
        var modules = new EngineModules();
        await using var engine = new global::app.@this("/app", modules);
        var context = engine.User.Context;

        var (action, error) = modules.GetCodeGenerated(new PrAction { Module = "nonexistent_xyz", ActionName = "nope" });

        await Assert.That(action).IsNull();
        await Assert.That(error).IsNotNull();
        await Assert.That(error!.Key).IsEqualTo("ActionNotFound");
    }

    [Test]
    public async Task GetCodeGenerated_RegisteredTwice_LastWins()
    {
        var modules = new EngineModules();
        var handler1 = new MockCodeGenHandler { Tag = "first" };
        var handler2 = new MockCodeGenHandler { Tag = "second" };
        modules.Register("custom", "run", handler1);
        modules.Register("custom", "run", handler2);

        await using var engine = new global::app.@this("/app", modules);
        var context = engine.User.Context;

        var (result, error) = modules.GetCodeGenerated(new PrAction { Module = "custom", ActionName = "run" });

        await Assert.That(error).IsNull();
        await Assert.That(((MockCodeGenHandler)result!).Tag).IsEqualTo("second");
    }

    [Test]
    public async Task GetCodeGenerated_TypeBased_CreatesNewInstance()
    {
        var modules = new EngineModules();
        await using var engine = new global::app.@this("/app", modules);
        var context = engine.User.Context;

        // variable.set is type-registered (discovered via [Action] attribute)
        var (action1, _) = modules.GetCodeGenerated(new PrAction { Module = "variable", ActionName = "set" });
        var (action2, _) = modules.GetCodeGenerated(new PrAction { Module = "variable", ActionName = "set" });

        // Per-call instantiation — different instances each time
        await Assert.That(action1).IsNotNull();
        await Assert.That(action2).IsNotNull();
        await Assert.That(ReferenceEquals(action1, action2)).IsFalse();
    }

    #endregion

    #region Discover

    [Test]
    public async Task Discover_NonMatchingNamespace_FindsNothing()
    {
        var modules = new EngineModules();
        modules.Clear(); // start fresh

        var count = modules.Discover(typeof(global::app.@this).Assembly, "Some.Completely.Wrong.Namespace");

        await Assert.That(count).IsEqualTo(0);
    }

    [Test]
    public async Task Discover_CorrectNamespace_FindsHandlers()
    {
        var modules = new EngineModules();
        modules.Clear(); // start fresh

        var count = modules.Discover(typeof(global::app.@this).Assembly, "app.modules");

        await Assert.That(modules.Contains("variable", "set")).IsTrue();
        await Assert.That(modules.Contains("output", "write")).IsTrue();
        await Assert.That(count).IsGreaterThan(0);
    }

    #endregion

    #region Aggregate queries

    [Test]
    public async Task Count_IncludesBuiltInAndRegistered()
    {
        var modules = new EngineModules();
        var countBefore = modules.Count;

        modules.Register("custom", "one", new MockHandler());
        modules.Register("custom", "two", new MockHandler());

        await Assert.That(modules.Count).IsEqualTo(countBefore + 2);
    }

    [Test]
    public async Task GetActionType_ReturnsTypeForBuiltIn()
    {
        var modules = new EngineModules();

        var type = modules.GetActionType("variable", "set");

        await Assert.That(type).IsNotNull();
    }

    [Test]
    public async Task GetActionType_ReturnsTypeForExplicitHandler()
    {
        var modules = new EngineModules();
        var handler = new MockCodeGenHandler();
        modules.Register("custom", "run", handler);

        var type = modules.GetActionType("custom", "run");

        await Assert.That(type).IsEqualTo(typeof(MockCodeGenHandler));
    }

    [Test]
    public async Task GetActionType_NonexistentAction_ReturnsNull()
    {
        var modules = new EngineModules();

        var type = modules.GetActionType("nonexistent_xyz", "nope");

        await Assert.That(type).IsNull();
    }

    [Test]
    public async Task RegisterType_RegistersTypeEntry()
    {
        var modules = new EngineModules();
        modules.RegisterType("custom", "run", typeof(MockCodeGenHandler));

        await Assert.That(modules.Contains("custom", "run")).IsTrue();
        await Assert.That(modules.GetActionType("custom", "run")).IsEqualTo(typeof(MockCodeGenHandler));
    }

    [Test]
    public async Task Names_IncludesRegistered_NoDuplicates()
    {
        var modules = new EngineModules();
        // "variable" already exists from built-in discovery
        modules.Register("variable", "custom_action", new MockHandler());
        modules.Register("exotic", "magic", new MockHandler());

        var names = modules.Names.ToList();

        await Assert.That(names).Contains("variable");
        await Assert.That(names).Contains("exotic");
        // "variable" should appear only once (flat registry, same key)
        await Assert.That(names.Count(m => m.Equals("variable", StringComparison.OrdinalIgnoreCase))).IsEqualTo(1);
    }

    [Test]
    public async Task GetActions_IncludesAll_NoDuplicates()
    {
        var modules = new EngineModules();
        // "variable.set" already exists from built-in
        modules.Register("variable", "set", new MockHandler()); // overwrites
        modules.Register("variable", "custom_action", new MockHandler()); // new

        var actions = modules.GetActions("variable").ToList();

        await Assert.That(actions).Contains("set");
        await Assert.That(actions).Contains("custom_action");
        // "set" should appear only once (flat registry, same key)
        await Assert.That(actions.Count(a => a.Equals("set", StringComparison.OrdinalIgnoreCase))).IsEqualTo(1);
    }

    #endregion

    #region Mock handlers

    /// <summary>
    /// IAction only — does NOT implement ICodeGenerated.
    /// Used to test the "handler doesn't implement ICodeGenerated" error path.
    /// </summary>
    private class MockHandler : IAction
    {
        public global::app.goal.steps.step.actions.action.@this Action { get; set; } = null!;
        public global::app.@this App { get; private set; } = null!;
        public global::app.actor.context.@this Context { get; private set; } = null!;
        public System.Type? ParameterType => null;
        public void Initialize(global::app.@this engine, global::app.actor.context.@this context) { App = engine; Context = context; }
        public Task<Data> ExecuteAsync(object? parameters) => Task.FromResult(Data.Ok());
    }

    /// <summary>
    /// IAction + ICodeGenerated — the correct handler interface.
    /// </summary>
    private class MockCodeGenHandler : IAction, ICodeGenerated
    {
        public global::app.goal.steps.step.actions.action.@this Action { get; set; } = null!;
        public string Tag { get; set; } = "";
        public global::app.@this App { get; private set; } = null!;
        public global::app.actor.context.@this Context { get; private set; } = null!;
        public System.Type? ParameterType => null;
        public void Initialize(global::app.@this engine, global::app.actor.context.@this context) { App = engine; Context = context; }
        public Task<Data> ExecuteAsync(global::app.goal.steps.step.actions.action.@this action, global::app.actor.context.@this context)
        {
            Initialize(context.App!, context);
            return Task.FromResult(Data.Ok());
        }
    }

    #endregion
}
