using PLang.Runtime2.Engine.Context;
using PLang.Runtime2.Engine;
using PLang.Runtime2.Engine.Memory;
using PLang.Runtime2.modules;

namespace PLang.Tests.Runtime2.modules;

public class LibrariesTests
{
    [Test]
    public async Task Constructor_DiscoversBultInHandlers()
    {
        var libraries = new Libraries();

        // Libraries constructor auto-discovers built-in handlers
        await Assert.That(libraries.Contains("variable", "set")).IsTrue();
        await Assert.That(libraries.Contains("output", "write")).IsTrue();
    }

    [Test]
    public async Task Register_AddsHandler()
    {
        var libraries = new Libraries();
        var handler = new MockHandler();

        libraries.Register("test", "do", handler);

        await Assert.That(libraries.Contains("test", "do")).IsTrue();
    }

    [Test]
    public async Task Get_ReturnsHandler()
    {
        var libraries = new Libraries();
        var handler = new MockHandler();
        libraries.Register("test", "do", handler);

        var result = libraries.Get("test", "do");

        await Assert.That(result).IsEqualTo(handler);
    }

    [Test]
    public async Task Get_CaseInsensitive()
    {
        var libraries = new Libraries();
        var handler = new MockHandler();
        libraries.Register("Test", "Do", handler);

        await Assert.That(libraries.Get("test", "do")).IsEqualTo(handler);
        await Assert.That(libraries.Get("TEST", "DO")).IsEqualTo(handler);
    }

    [Test]
    public async Task Get_NonexistentHandler_ReturnsNull()
    {
        var libraries = new Libraries();

        await Assert.That(libraries.Get("nonexistent", "method")).IsNull();
    }

    [Test]
    public async Task Contains_WithModuleAndAction_ReturnsTrue()
    {
        var libraries = new Libraries();
        libraries.Register("test", "do", new MockHandler());

        await Assert.That(libraries.Contains("test", "do")).IsTrue();
    }

    [Test]
    public async Task Contains_WithModuleOnly_ReturnsTrue()
    {
        var libraries = new Libraries();
        libraries.Register("test", "do", new MockHandler());

        await Assert.That(libraries.Contains("test")).IsTrue();
    }

    [Test]
    public async Task Contains_NonexistentModule_ReturnsFalse()
    {
        var libraries = new Libraries();

        await Assert.That(libraries.Contains("nonexistent_xyz_123")).IsFalse();
    }

    [Test]
    public async Task GetActions_ReturnsAllActionsInModule()
    {
        var libraries = new Libraries();
        libraries.Register("custom", "alpha", new MockHandler());
        libraries.Register("custom", "beta", new MockHandler());

        var actions = libraries.GetActions("custom").ToList();

        await Assert.That(actions).Contains("alpha");
        await Assert.That(actions).Contains("beta");
    }

    [Test]
    public async Task GetActions_NonexistentModule_ReturnsEmpty()
    {
        var libraries = new Libraries();

        var actions = libraries.GetActions("nonexistent_xyz_123").ToList();

        await Assert.That(actions.Count).IsEqualTo(0);
    }

    [Test]
    public async Task Modules_ReturnsAllModules()
    {
        var libraries = new Libraries();

        var modules = libraries.Modules.ToList();

        // Built-in modules should be present
        await Assert.That(modules).Contains("variable");
        await Assert.That(modules).Contains("output");
    }

    [Test]
    public async Task Clear_RemovesAllHandlers()
    {
        var libraries = new Libraries();
        libraries.Register("custom", "do", new MockHandler());

        libraries.Clear();

        await Assert.That(libraries.Contains("custom")).IsFalse();
        // Built-in handlers also cleared
        await Assert.That(libraries.Contains("variable")).IsFalse();
    }

    [Test]
    public async Task Register_SameKeyTwice_ReplacesHandler()
    {
        var libraries = new Libraries();
        var handler1 = new MockHandler();
        var handler2 = new MockHandler();
        libraries.Register("test", "do", handler1);

        libraries.Register("test", "do", handler2);

        await Assert.That(libraries.Get("test", "do")).IsEqualTo(handler2);
    }

    [Test]
    public async Task BuiltIn_DiscoversFindHandlers()
    {
        var libraries = new Libraries();

        // Should discover variable.set, variable.get, etc.
        await Assert.That(libraries.Contains("variable", "set")).IsTrue();
        await Assert.That(libraries.Contains("variable", "get")).IsTrue();
        await Assert.That(libraries.Contains("variable", "remove")).IsTrue();
        await Assert.That(libraries.Contains("variable", "exists")).IsTrue();
        await Assert.That(libraries.Contains("variable", "clear")).IsTrue();
        await Assert.That(libraries.Contains("output", "write")).IsTrue();
        await Assert.That(libraries.Contains("file", "save")).IsTrue();
        await Assert.That(libraries.Contains("file", "read")).IsTrue();
        await Assert.That(libraries.Contains("file", "delete")).IsTrue();
        await Assert.That(libraries.Contains("file", "exists")).IsTrue();
        await Assert.That(libraries.Contains("file", "copy")).IsTrue();
        await Assert.That(libraries.Contains("file", "move")).IsTrue();
    }

    [Test]
    public async Task All_ReturnsRegisteredHandlers()
    {
        var libraries = new Libraries();
        var handler1 = new MockHandler();
        var handler2 = new MockHandler();
        libraries.Register("ns1", "cls1", handler1);
        libraries.Register("ns2", "cls2", handler2);

        var all = libraries.All.ToList();

        await Assert.That(all).Contains(handler1);
        await Assert.That(all).Contains(handler2);
    }

    [Test]
    public async Task Library_Standalone_StartsEmpty()
    {
        var library = new Library("test");

        await Assert.That(library.Count).IsEqualTo(0);
        await Assert.That(library.Name).IsEqualTo("test");
    }

    [Test]
    public async Task Library_Get_NullOrEmpty_ReturnsNull()
    {
        var library = new Library("test");

        await Assert.That(library.Get(null!, "do")).IsNull();
        await Assert.That(library.Get("", "do")).IsNull();
        await Assert.That(library.Get("test", null!)).IsNull();
        await Assert.That(library.Get("test", "")).IsNull();
    }

    [Test]
    public async Task AddLibrary_ResolvesFromAddedLibrary()
    {
        var libraries = new Libraries();
        var external = new Library("external");
        external.Register("custom", "magic", new MockHandler());
        libraries.Add(external);

        await Assert.That(libraries.Contains("custom", "magic")).IsTrue();
    }

    [Test]
    public async Task Value_ReturnsAllLibraries()
    {
        var libraries = new Libraries();
        var external = new Library("external");
        libraries.Add(external);

        await Assert.That(libraries.Value.Count).IsEqualTo(2); // builtin + external
        await Assert.That(libraries[0].Name).IsEqualTo("builtin");
        await Assert.That(libraries[1].Name).IsEqualTo("external");
    }

    #region Libraries.GetCodeGenerated

    [Test]
    public async Task GetCodeGenerated_BuiltInAction_ReturnsHandler()
    {
        var libraries = new Libraries();
        await using var engine = new Engine("/app", libraries);
        using var context = engine.CreateContext();

        var (handler, error) = libraries.GetCodeGenerated("variable", "set", context);

        await Assert.That(handler).IsNotNull();
        await Assert.That(error).IsNull();
    }

    [Test]
    public async Task GetCodeGenerated_ExplicitCodeGenHandler_ReturnsHandler()
    {
        var libraries = new Libraries();
        var handler = new MockCodeGenHandler();
        libraries.Register("custom", "run", handler);
        await using var engine = new Engine("/app", libraries);
        using var context = engine.CreateContext();

        var (result, error) = libraries.GetCodeGenerated("custom", "run", context);

        await Assert.That(result).IsEqualTo(handler);
        await Assert.That(error).IsNull();
    }

    [Test]
    public async Task GetCodeGenerated_NonICodeGeneratedHandler_ReturnsHandlerError()
    {
        var libraries = new Libraries();
        libraries.Register("legacy", "do", new MockHandler());
        await using var engine = new Engine("/app", libraries);
        using var context = engine.CreateContext();

        var (handler, error) = libraries.GetCodeGenerated("legacy", "do", context);

        await Assert.That(handler).IsNull();
        await Assert.That(error).IsNotNull();
        await Assert.That(error!.Key).IsEqualTo("HandlerError");
    }

    [Test]
    public async Task GetCodeGenerated_NotFound_ReturnsActionNotFound()
    {
        var libraries = new Libraries();
        await using var engine = new Engine("/app", libraries);
        using var context = engine.CreateContext();

        var (handler, error) = libraries.GetCodeGenerated("nonexistent_xyz", "nope", context);

        await Assert.That(handler).IsNull();
        await Assert.That(error).IsNotNull();
        await Assert.That(error!.Key).IsEqualTo("ActionNotFound");
    }

    [Test]
    public async Task GetCodeGenerated_MultiLibrary_FirstMatchWins()
    {
        var libraries = new Libraries();
        var builtInHandler = new MockCodeGenHandler { Tag = "builtin" };
        libraries.Register("custom", "run", builtInHandler);

        var external = new Library("external");
        var externalHandler = new MockCodeGenHandler { Tag = "external" };
        external.Register("custom", "run", externalHandler);
        libraries.Add(external);

        await using var engine = new Engine("/app", libraries);
        using var context = engine.CreateContext();

        var (result, error) = libraries.GetCodeGenerated("custom", "run", context);

        await Assert.That(error).IsNull();
        // BuiltIn is [0], so it wins
        await Assert.That(((MockCodeGenHandler)result!).Tag).IsEqualTo("builtin");
    }

    [Test]
    public async Task GetCodeGenerated_FallsToSecondLibrary_WhenFirstDoesNotHaveIt()
    {
        var libraries = new Libraries();

        var external = new Library("external");
        var externalHandler = new MockCodeGenHandler { Tag = "external" };
        external.Register("exotic", "magic", externalHandler);
        libraries.Add(external);

        await using var engine = new Engine("/app", libraries);
        using var context = engine.CreateContext();

        var (result, error) = libraries.GetCodeGenerated("exotic", "magic", context);

        await Assert.That(error).IsNull();
        await Assert.That(((MockCodeGenHandler)result!).Tag).IsEqualTo("external");
    }

    [Test]
    public async Task GetCodeGenerated_TypeBased_CreatesNewInstance()
    {
        var libraries = new Libraries();
        await using var engine = new Engine("/app", libraries);
        using var context = engine.CreateContext();

        // variable.set is type-registered (discovered via [Action] attribute)
        var (handler1, _) = libraries.GetCodeGenerated("variable", "set", context);
        var (handler2, _) = libraries.GetCodeGenerated("variable", "set", context);

        // Per-call instantiation — different instances each time
        await Assert.That(handler1).IsNotNull();
        await Assert.That(handler2).IsNotNull();
        await Assert.That(ReferenceEquals(handler1, handler2)).IsFalse();
    }

    #endregion

    #region Library.GetCodeGenerated

    [Test]
    public async Task Library_GetCodeGenerated_ExplicitCodeGenHandler_ReturnsIt()
    {
        var library = new Library("test");
        var handler = new MockCodeGenHandler();
        library.Register("mod", "act", handler);

        var result = library.GetCodeGenerated("mod", "act");

        await Assert.That(result).IsEqualTo(handler);
    }

    [Test]
    public async Task Library_GetCodeGenerated_ExplicitNonCodeGen_ReturnsNull()
    {
        var library = new Library("test");
        library.Register("mod", "act", new MockHandler());

        var result = library.GetCodeGenerated("mod", "act");

        // MockHandler does not implement ICodeGenerated, so returns null
        await Assert.That(result).IsNull();
    }

    [Test]
    public async Task Library_GetCodeGenerated_TypeRegistered_CreatesInstance()
    {
        var library = new Library("test");
        library.RegisterCodeGenerated("mod", "act", typeof(MockCodeGenHandler));

        var result = library.GetCodeGenerated("mod", "act");

        await Assert.That(result).IsNotNull();
        await Assert.That(result).IsTypeOf<MockCodeGenHandler>();
    }

    [Test]
    public async Task Library_GetCodeGenerated_NotFound_ReturnsNull()
    {
        var library = new Library("test");

        var result = library.GetCodeGenerated("nonexistent", "nope");

        await Assert.That(result).IsNull();
    }

    [Test]
    public async Task Library_GetCodeGenerated_TypeNotICodeGenerated_ReturnsNull()
    {
        var library = new Library("test");
        library.RegisterCodeGenerated("mod", "act", typeof(MockHandler));

        var result = library.GetCodeGenerated("mod", "act");

        await Assert.That(result).IsNull();
    }

    #endregion

    #region Library.Discover

    [Test]
    public async Task Library_Discover_NullAssembly_IsNoOp()
    {
        var library = new Library("test", assembly: null);

        library.Discover();

        await Assert.That(library.Count).IsEqualTo(0);
    }

    [Test]
    public async Task Library_Discover_NonMatchingNamespace_FindsNothing()
    {
        var library = new Library("test", typeof(Engine).Assembly);

        library.Discover("Some.Completely.Wrong.Namespace");

        await Assert.That(library.Count).IsEqualTo(0);
    }

    [Test]
    public async Task Library_Discover_CorrectNamespace_FindsHandlers()
    {
        var library = new Library("test", typeof(Engine).Assembly);

        library.Discover("PLang.Runtime2.modules");

        await Assert.That(library.Contains("variable", "set")).IsTrue();
        await Assert.That(library.Contains("output", "write")).IsTrue();
        await Assert.That(library.Count).IsGreaterThan(0);
    }

    #endregion

    #region Libraries aggregate queries

    [Test]
    public async Task Count_IncludesBuiltInAndRegistered()
    {
        var libraries = new Libraries();
        var countBefore = libraries.Count;

        libraries.Register("custom", "one", new MockHandler());
        libraries.Register("custom", "two", new MockHandler());

        await Assert.That(libraries.Count).IsEqualTo(countBefore + 2);
    }

    [Test]
    public async Task Count_SpansMultipleLibraries()
    {
        var libraries = new Libraries();
        var countBefore = libraries.Count;

        var external = new Library("external");
        external.Register("ext", "alpha", new MockHandler());
        external.Register("ext", "beta", new MockHandler());
        libraries.Add(external);

        await Assert.That(libraries.Count).IsEqualTo(countBefore + 2);
    }

    [Test]
    public async Task GetActionType_ReturnsTypeForBuiltIn()
    {
        var libraries = new Libraries();

        var type = libraries.GetActionType("variable", "set");

        await Assert.That(type).IsNotNull();
    }

    [Test]
    public async Task GetActionType_ReturnsTypeForExplicitHandler()
    {
        var libraries = new Libraries();
        var handler = new MockCodeGenHandler();
        libraries.Register("custom", "run", handler);

        var type = libraries.GetActionType("custom", "run");

        await Assert.That(type).IsEqualTo(typeof(MockCodeGenHandler));
    }

    [Test]
    public async Task GetActionType_NonexistentAction_ReturnsNull()
    {
        var libraries = new Libraries();

        var type = libraries.GetActionType("nonexistent_xyz", "nope");

        await Assert.That(type).IsNull();
    }

    [Test]
    public async Task RegisterCodeGenerated_RegistersTypeOnBuiltIn()
    {
        var libraries = new Libraries();
        libraries.RegisterCodeGenerated("custom", "run", typeof(MockCodeGenHandler));

        await Assert.That(libraries.Contains("custom", "run")).IsTrue();
        await Assert.That(libraries.GetActionType("custom", "run")).IsEqualTo(typeof(MockCodeGenHandler));
    }

    [Test]
    public async Task Modules_IncludesAcrossLibraries_NoDuplicates()
    {
        var libraries = new Libraries();
        // "variable" already exists in builtin
        var external = new Library("external");
        external.Register("variable", "custom_action", new MockHandler());
        external.Register("exotic", "magic", new MockHandler());
        libraries.Add(external);

        var modules = libraries.Modules.ToList();

        await Assert.That(modules).Contains("variable");
        await Assert.That(modules).Contains("exotic");
        // "variable" should appear only once
        await Assert.That(modules.Count(m => m.Equals("variable", StringComparison.OrdinalIgnoreCase))).IsEqualTo(1);
    }

    [Test]
    public async Task GetActions_IncludesAcrossLibraries_NoDuplicates()
    {
        var libraries = new Libraries();
        // "variable.set" already exists in builtin
        var external = new Library("external");
        external.Register("variable", "set", new MockHandler()); // duplicate
        external.Register("variable", "custom_action", new MockHandler()); // new
        libraries.Add(external);

        var actions = libraries.GetActions("variable").ToList();

        await Assert.That(actions).Contains("set");
        await Assert.That(actions).Contains("custom_action");
        // "set" should appear only once
        await Assert.That(actions.Count(a => a.Equals("set", StringComparison.OrdinalIgnoreCase))).IsEqualTo(1);
    }

    #endregion

    #region Library standalone tests

    [Test]
    public async Task Library_Register_And_Contains()
    {
        var library = new Library("test");
        library.Register("mod", "act", new MockHandler());

        await Assert.That(library.Contains("mod", "act")).IsTrue();
        await Assert.That(library.Contains("mod")).IsTrue();
        await Assert.That(library.Contains("other")).IsFalse();
        await Assert.That(library.Contains("mod", "other")).IsFalse();
    }

    [Test]
    public async Task Library_GetActions_ReturnsAll()
    {
        var library = new Library("test");
        library.Register("mod", "alpha", new MockHandler());
        library.Register("mod", "beta", new MockHandler());
        library.RegisterCodeGenerated("mod", "gamma", typeof(MockCodeGenHandler));

        var actions = library.GetActions("mod").ToList();

        await Assert.That(actions).Contains("alpha");
        await Assert.That(actions).Contains("beta");
        await Assert.That(actions).Contains("gamma");
    }

    [Test]
    public async Task Library_Modules_ReturnsAll()
    {
        var library = new Library("test");
        library.Register("aaa", "x", new MockHandler());
        library.RegisterCodeGenerated("bbb", "y", typeof(MockCodeGenHandler));

        var modules = library.Modules.ToList();

        await Assert.That(modules).Contains("aaa");
        await Assert.That(modules).Contains("bbb");
    }

    [Test]
    public async Task Library_GetActionType_ExplicitHandler()
    {
        var library = new Library("test");
        var handler = new MockCodeGenHandler();
        library.Register("mod", "act", handler);

        var type = library.GetActionType("mod", "act");

        await Assert.That(type).IsEqualTo(typeof(MockCodeGenHandler));
    }

    [Test]
    public async Task Library_GetActionType_TypeRegistered()
    {
        var library = new Library("test");
        library.RegisterCodeGenerated("mod", "act", typeof(MockCodeGenHandler));

        var type = library.GetActionType("mod", "act");

        await Assert.That(type).IsEqualTo(typeof(MockCodeGenHandler));
    }

    [Test]
    public async Task Library_GetActionType_NotFound_ReturnsNull()
    {
        var library = new Library("test");

        var type = library.GetActionType("nope", "nope");

        await Assert.That(type).IsNull();
    }

    [Test]
    public async Task Library_All_OnlyExplicitHandlers()
    {
        var library = new Library("test");
        var handler = new MockHandler();
        library.Register("mod", "act", handler);
        library.RegisterCodeGenerated("mod", "type_act", typeof(MockCodeGenHandler));

        var all = library.All.ToList();

        // All only yields explicit instances, not type-registered ones
        await Assert.That(all).Contains(handler);
        await Assert.That(all.Count).IsEqualTo(1);
    }

    [Test]
    public async Task Library_Clear_RemovesEverything()
    {
        var library = new Library("test");
        library.Register("mod", "act", new MockHandler());
        library.RegisterCodeGenerated("mod", "type_act", typeof(MockCodeGenHandler));

        library.Clear();

        await Assert.That(library.Count).IsEqualTo(0);
        await Assert.That(library.Contains("mod")).IsFalse();
    }

    [Test]
    public async Task Library_Assembly_Property()
    {
        var assembly = typeof(Engine).Assembly;
        var library = new Library("test", assembly);

        await Assert.That(library.Assembly).IsEqualTo(assembly);
    }

    [Test]
    public async Task Library_Assembly_NullByDefault()
    {
        var library = new Library("test");

        await Assert.That(library.Assembly).IsNull();
    }

    #endregion

    #region Mock handlers

    /// <summary>
    /// IClass only — does NOT implement ICodeGenerated.
    /// Used to test the "handler doesn't implement ICodeGenerated" error path.
    /// </summary>
    private class MockHandler : IClass
    {
        public Engine Engine { get; private set; } = null!;
        public PLangContext Context { get; private set; } = null!;
        public System.Type? ParameterType => null;
        public void Initialize(Engine engine, PLangContext context) { Engine = engine; Context = context; }
        public Task<Data> ExecuteAsync(object? parameters) => Task.FromResult(Data.Ok());
    }

    /// <summary>
    /// IClass + ICodeGenerated — the correct handler interface.
    /// </summary>
    private class MockCodeGenHandler : IClass, ICodeGenerated
    {
        public string Tag { get; set; } = "";
        public Engine Engine { get; private set; } = null!;
        public PLangContext Context { get; private set; } = null!;
        public System.Type? ParameterType => null;
        public void Initialize(Engine engine, PLangContext context) { Engine = engine; Context = context; }
        public Task<Data> ExecuteAsync(object? parameters) => Task.FromResult(Data.Ok());
        public Task<Data> CodeGeneratedExecuteAsync(List<Data> parameters, Engine engine, PLangContext context)
        {
            Initialize(engine, context);
            return ExecuteAsync(null);
        }
    }

    #endregion
}
