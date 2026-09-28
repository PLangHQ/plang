using app.actor.context;
using app.type.item.variable;
using app.module;

namespace PLang.Tests.App.actions;

public class LibrariesTests
{
    [Test]
    public async Task Constructor_DiscoversBultInHandlers()
    {
        await using var modulesHost = TestApp.Create("/app");

        // the app's module auto-discovers built-in handlers
        await Assert.That(modulesHost.Module("variable")["set"] != null).IsTrue();
        await Assert.That(modulesHost.Module("output")["write"] != null).IsTrue();
    }

    [Test]
    public async Task Register_AddsHandler()
    {
        await using var modulesHost = TestApp.Create("/app");

        modulesHost.module.Register("test", "do", typeof(MockHandler));

        await Assert.That(modulesHost.Module("test")["do"] != null).IsTrue();
    }

    [Test]
    public async Task Register_CaseInsensitive()
    {
        await using var modulesHost = TestApp.Create("/app");
        modulesHost.module.Register("Test", "Do", typeof(MockHandler));

        await Assert.That(modulesHost.Module("test")["do"] != null).IsTrue();
        await Assert.That(modulesHost.Module("TEST")["DO"] != null).IsTrue();
    }

    [Test]
    public async Task Contains_WithModuleOnly_ReturnsTrue()
    {
        await using var modulesHost = TestApp.Create("/app");
        modulesHost.module.Register("test", "do", typeof(MockHandler));

        await Assert.That((await modulesHost.module.Get("test")).Success).IsTrue();
    }

    [Test]
    public async Task Contains_NonexistentModule_ReturnsFalse()
    {
        await using var modulesHost = TestApp.Create("/app");

        await Assert.That((await modulesHost.module.Get("nonexistent_xyz_123")).Success).IsFalse();
    }

    [Test]
    public async Task GetActions_ReturnsAllActionsInModule()
    {
        await using var modulesHost = TestApp.Create("/app");
        modulesHost.module.Register("custom", "alpha", typeof(MockHandler));
        modulesHost.module.Register("custom", "beta", typeof(MockHandler));

        var actions = modulesHost.Module("custom").ActionNames.ToList();

        await Assert.That(actions).Contains("alpha");
        await Assert.That(actions).Contains("beta");
    }

    [Test]
    public async Task GetActions_NonexistentModule_ReturnsEmpty()
    {
        await using var modulesHost = TestApp.Create("/app");

        var actions = modulesHost.Module("nonexistent_xyz_123").ActionNames.ToList();

        await Assert.That(actions.Count).IsEqualTo(0);
    }

    [Test]
    public async Task Names_ReturnsAllModules()
    {
        await using var modulesHost = TestApp.Create("/app");

        var names = modulesHost.module.list.Items().Select(m => m.Name).ToList();

        // Built-in modules should be present
        await Assert.That(names).Contains("variable");
        await Assert.That(names).Contains("output");
    }

    [Test]
    public async Task Register_SameKeyTwice_ReplacesHandler()
    {
        await using var modulesHost = TestApp.Create("/app");
        modulesHost.module.Register("test", "do", typeof(MockHandler));

        modulesHost.module.Register("test", "do", typeof(MockCodeGenHandler));

        await Assert.That(modulesHost.Module("test").Handler("do")).IsEqualTo(typeof(MockCodeGenHandler));
    }

    [Test]
    public async Task BuiltIn_DiscoversFindHandlers()
    {
        await using var modulesHost = TestApp.Create("/app");

        // Should discover variable.set, variable.get, etc.
        await Assert.That(modulesHost.Module("variable")["set"] != null).IsTrue();
        await Assert.That(modulesHost.Module("variable")["get"] != null).IsTrue();
        await Assert.That(modulesHost.Module("variable")["remove"] != null).IsTrue();
        await Assert.That(modulesHost.Module("variable")["exists"] != null).IsTrue();
        await Assert.That(modulesHost.Module("variable")["clear"] != null).IsTrue();
        await Assert.That(modulesHost.Module("output")["write"] != null).IsTrue();
        await Assert.That(modulesHost.Module("file")["save"] != null).IsTrue();
        await Assert.That(modulesHost.Module("file")["read"] != null).IsTrue();
        await Assert.That(modulesHost.Module("file")["delete"] != null).IsTrue();
        await Assert.That(modulesHost.Module("file")["exists"] != null).IsTrue();
        await Assert.That(modulesHost.Module("file")["copy"] != null).IsTrue();
        await Assert.That(modulesHost.Module("file")["move"] != null).IsTrue();
    }

    // The catalog action holds the class that runs it.
    [Test]
    public async Task Register_CatalogActionHoldsItsClass()
    {
        await using var modulesHost = TestApp.Create("/app");
        modulesHost.module.Register("custom", "magic", typeof(MockCodeGenHandler));

        await Assert.That(modulesHost.Module("custom")["magic"]!.Class).IsEqualTo(typeof(MockCodeGenHandler));
    }

    #region action.Instance

    [Test]
    public async Task GetCodeGenerated_BuiltInAction_ReturnsAction()
    {
        await using var engine = TestApp.Create("/app");

        var (action, error) = (new PrAction { Module = engine.Module("variable"), Name = "set" }).Instance(global::PLang.Tests.TestApp.SharedContext);

        await Assert.That(action).IsNotNull();
        await Assert.That(error).IsNull();
    }

    [Test]
    public async Task GetCodeGenerated_RegisteredClass_ReturnsItsInstance()
    {
        await using var engine = TestApp.Create("/app");
        engine.module.Register("custom", "run", typeof(MockCodeGenHandler));

        var (result, error) = (new PrAction { Module = engine.Module("custom"), Name = "run" }).Instance(global::PLang.Tests.TestApp.SharedContext);

        await Assert.That(result).IsTypeOf<MockCodeGenHandler>();
        await Assert.That(error).IsNull();
    }

    [Test]
    public async Task GetCodeGenerated_NonICodeGeneratedAction_ReturnsActionError()
    {
        await using var engine = TestApp.Create("/app");
        engine.module.Register("legacy", "do", typeof(MockHandler));

        var (action, error) = (new PrAction { Module = engine.Module("legacy"), Name = "do" }).Instance(global::PLang.Tests.TestApp.SharedContext);

        await Assert.That(action).IsNull();
        await Assert.That(error).IsNotNull();
        await Assert.That(error!.Key).IsEqualTo("ActionError");
    }

    [Test]
    public async Task GetCodeGenerated_NotFound_ReturnsActionNotFound()
    {
        await using var engine = TestApp.Create("/app");

        var (action, error) = (new PrAction { Module = engine.Module("variable"), Name = "nope" }).Instance(global::PLang.Tests.TestApp.SharedContext);

        await Assert.That(action).IsNull();
        await Assert.That(error).IsNotNull();
        await Assert.That(error!.Key).IsEqualTo("ActionNotFound");
    }

    [Test]
    public async Task GetCodeGenerated_RegisteredTwice_LastWins()
    {
        await using var engine = TestApp.Create("/app");
        engine.module.Register("custom", "run", typeof(MockHandler));
        engine.module.Register("custom", "run", typeof(MockCodeGenHandler));

        var (result, error) = (new PrAction { Module = engine.Module("custom"), Name = "run" }).Instance(global::PLang.Tests.TestApp.SharedContext);

        await Assert.That(error).IsNull();
        await Assert.That(result).IsTypeOf<MockCodeGenHandler>();
    }

    [Test]
    public async Task GetCodeGenerated_TypeBased_CreatesNewInstance()
    {
        await using var engine = TestApp.Create("/app");

        // variable.set is type-registered (discovered via [Action] attribute)
        var (action1, _) = (new PrAction { Module = engine.Module("variable"), Name = "set" }).Instance(global::PLang.Tests.TestApp.SharedContext);
        var (action2, _) = (new PrAction { Module = engine.Module("variable"), Name = "set" }).Instance(global::PLang.Tests.TestApp.SharedContext);

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
        await using var modulesHost = TestApp.Create("/app");

        var count = modulesHost.module.Discover(typeof(global::app.@this).Assembly, "Some.Completely.Wrong.Namespace");

        await Assert.That(count).IsEqualTo(0);
    }

    [Test]
    public async Task Discover_CorrectNamespace_FindsHandlers()
    {
        await using var modulesHost = TestApp.Create("/app");

        var count = modulesHost.module.Discover(typeof(global::app.@this).Assembly, "app.module");

        await Assert.That(modulesHost.Module("variable")["set"] != null).IsTrue();
        await Assert.That(modulesHost.Module("output")["write"] != null).IsTrue();
        await Assert.That(count).IsGreaterThan(0);
    }

    #endregion

    #region Aggregate queries

    [Test]
    public async Task Count_IncludesBuiltInAndRegistered()
    {
        await using var modulesHost = TestApp.Create("/app");
        var modules = modulesHost.module.list;
        var countBefore = modules.Items().Sum(m => m.Count);

        modulesHost.module.Register("custom", "one", typeof(MockHandler));
        modulesHost.module.Register("custom", "two", typeof(MockHandler));

        await Assert.That(modules.Items().Sum(m => m.Count)).IsEqualTo(countBefore + 2);
    }

    [Test]
    public async Task GetActionType_ReturnsTypeForBuiltIn()
    {
        await using var modulesHost = TestApp.Create("/app");

        var type = modulesHost.Module("variable").Handler("set");

        await Assert.That(type).IsNotNull();
    }

    [Test]
    public async Task GetActionType_NonexistentAction_ReturnsNull()
    {
        await using var modulesHost = TestApp.Create("/app");

        var type = modulesHost.Module("nonexistent_xyz").Handler("nope");

        await Assert.That(type).IsNull();
    }

    [Test]
    public async Task RegisterType_RegistersTypeEntry()
    {
        await using var modulesHost = TestApp.Create("/app");
        modulesHost.module.Register("custom", "run", typeof(MockCodeGenHandler));

        await Assert.That(modulesHost.Module("custom")["run"] != null).IsTrue();
        await Assert.That(modulesHost.Module("custom").Handler("run")).IsEqualTo(typeof(MockCodeGenHandler));
    }

    [Test]
    public async Task Names_IncludesRegistered_NoDuplicates()
    {
        await using var modulesHost = TestApp.Create("/app");
        // "variable" already exists from built-in discovery
        modulesHost.module.Register("variable", "custom_action", typeof(MockHandler));
        modulesHost.module.Register("exotic", "magic", typeof(MockHandler));

        var names = modulesHost.module.list.Items().Select(m => m.Name).ToList();

        await Assert.That(names).Contains("variable");
        await Assert.That(names).Contains("exotic");
        // "variable" should appear only once (flat registry, same key)
        await Assert.That(names.Count(m => m.Equals("variable", StringComparison.OrdinalIgnoreCase))).IsEqualTo(1);
    }

    [Test]
    public async Task GetActions_IncludesAll_NoDuplicates()
    {
        await using var modulesHost = TestApp.Create("/app");
        // "variable.set" already exists from built-in
        modulesHost.module.Register("variable", "set", typeof(MockHandler)); // overwrites
        modulesHost.module.Register("variable", "custom_action", typeof(MockHandler)); // new

        var actions = modulesHost.Module("variable").ActionNames.ToList();

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
        public global::app.goal.step.action.@this Action { get; set; } = null!;
        public global::app.@this App { get; private set; } = null!;
        public global::app.actor.context.@this Context { get; private set; } = null!;
        public System.Type? ParameterType => null;
        public void Initialize(global::app.@this engine, global::app.actor.context.@this context) { App = engine; Context = context; }
        public Task<Data> ExecuteAsync(object? parameters) => Task.FromResult(Data.Ok());
    }

    /// <summary>
    /// IAction + ICodeGenerated — the correct handler interface, born with its run's context.
    /// </summary>
    private class MockCodeGenHandler : IAction, ICodeGenerated
    {
        public MockCodeGenHandler(global::app.actor.context.@this context) => Initialize(context.App!, context);

        public global::app.goal.step.action.@this Action { get; set; } = null!;
        public global::app.@this App { get; private set; } = null!;
        public global::app.actor.context.@this Context { get; private set; } = null!;
        public System.Type? ParameterType => null;
        public void Initialize(global::app.@this engine, global::app.actor.context.@this context) { App = engine; Context = context; }
        public Task<global::app.error.Error?> Attach(global::app.goal.step.action.@this action, global::app.actor.context.@this context)
        { Action = action; Initialize(context.App!, context); return Task.FromResult<global::app.error.Error?>(null); }
        public Task<Data> Start() => Task.FromResult(Data.Ok());
    }

    #endregion
}
