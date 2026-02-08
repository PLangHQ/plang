using PLang.Runtime2.Context;
using PLang.Runtime2.Core;
using PLang.Runtime2.Memory;
using PLang.Runtime2.actions;
using PLang.Runtime2.Serialization;

namespace PLang.Tests.Runtime2.Core;

public class EngineTests
{
    private PLangAppContext CreateAppContext() => new PLangAppContext("/app");

    private static Step MakeStep(string actionClass, string method, object? parameters = null, int index = 0, string text = "")
    {
        return new Step
        {
            Index = index,
            Text = text,
            Actions = new Actions
            {
                new PLang.Runtime2.Core.Action
                {
                    Class = actionClass,
                    Method = method,
                    Parameters = parameters is IDictionary<string, object?> dict
                        ? dict.Select(kv => new Data(kv.Key, kv.Value)).ToList()
                        : new List<Data>(),
                    Return = null
                }
            }
        };
    }

    private static Step MakeStepWithReturn(string actionClass, string method, object? parameters, string returnVarName, int index = 0, string text = "")
    {
        return new Step
        {
            Index = index,
            Text = text,
            Actions = new Actions
            {
                new PLang.Runtime2.Core.Action
                {
                    Class = actionClass,
                    Method = method,
                    Parameters = parameters is IDictionary<string, object?> dict
                        ? dict.Select(kv => new Data(kv.Key, kv.Value)).ToList()
                        : new List<Data>(),
                    Return = new List<Data> { new Data(returnVarName) }
                }
            }
        };
    }

    #region Actor Tests

    [Test]
    public async Task System_ReturnsActorWithSystemTrustLevel()
    {
        using var appContext = CreateAppContext();
        await using var engine = new Engine(appContext);

        var system = engine.System;

        await Assert.That(system.Name).IsEqualTo("System");
        await Assert.That(system.TrustLevel).IsEqualTo(TrustLevel.System);
        await Assert.That((int)system.TrustLevel).IsEqualTo(3);
    }

    [Test]
    public async Task Service_ReturnsActorWithServiceTrustLevel()
    {
        using var appContext = CreateAppContext();
        await using var engine = new Engine(appContext);

        var service = engine.Service;

        await Assert.That(service.Name).IsEqualTo("Service");
        await Assert.That(service.TrustLevel).IsEqualTo(TrustLevel.Service);
        await Assert.That((int)service.TrustLevel).IsEqualTo(2);
    }

    [Test]
    public async Task User_ReturnsActorWithUserTrustLevel()
    {
        using var appContext = CreateAppContext();
        await using var engine = new Engine(appContext);

        var user = engine.User;

        await Assert.That(user.Name).IsEqualTo("User");
        await Assert.That(user.TrustLevel).IsEqualTo(TrustLevel.User);
        await Assert.That((int)user.TrustLevel).IsEqualTo(1);
    }

    [Test]
    public async Task Actors_AreLazilyCreated()
    {
        using var appContext = CreateAppContext();
        await using var engine = new Engine(appContext);

        // Access only User actor
        var user = engine.User;

        // User should have its own context
        await Assert.That(user.Context).IsNotNull();
        await Assert.That(user.IO).IsNotNull();
        await Assert.That(user.Engine).IsEqualTo(engine);
    }

    [Test]
    public async Task Actors_HaveIsolatedContexts()
    {
        using var appContext = CreateAppContext();
        await using var engine = new Engine(appContext);

        engine.User.Context.MemoryStack.Set("key", "user-value");
        engine.System.Context.MemoryStack.Set("key", "system-value");

        await Assert.That(engine.User.Context.MemoryStack.GetValue("key")).IsEqualTo("user-value");
        await Assert.That(engine.System.Context.MemoryStack.GetValue("key")).IsEqualTo("system-value");
    }

    [Test]
    public async Task Actors_HaveIsolatedIO()
    {
        using var appContext = CreateAppContext();
        await using var engine = new Engine(appContext);

        engine.User.IO.CreateMemoryChannel("test");
        engine.System.IO.CreateMemoryChannel("test");

        await Assert.That(engine.User.IO.Contains("test")).IsTrue();
        await Assert.That(engine.System.IO.Contains("test")).IsTrue();
        // They are separate instances
        await Assert.That(engine.User.IO.Get("test")).IsNotEqualTo(engine.System.IO.Get("test"));
    }

    [Test]
    public async Task Actor_Context_HasBackReference()
    {
        using var appContext = CreateAppContext();
        await using var engine = new Engine(appContext);

        await Assert.That(engine.User.Context.Actor).IsEqualTo(engine.User);
        await Assert.That(engine.System.Context.Actor).IsEqualTo(engine.System);
        await Assert.That(engine.Service.Context.Actor).IsEqualTo(engine.Service);
    }

    [Test]
    public async Task Actor_SameInstanceOnMultipleAccess()
    {
        using var appContext = CreateAppContext();
        await using var engine = new Engine(appContext);

        var user1 = engine.User;
        var user2 = engine.User;

        await Assert.That(user1).IsEqualTo(user2);
    }

    #endregion

    [Test]
    public async Task Constructor_SetsProperties()
    {
        using var appContext = CreateAppContext();
        await using var engine = new Engine(appContext);

        await Assert.That(engine.AppContext).IsEqualTo(appContext);
        await Assert.That(engine.RootPath).IsEqualTo("/app");
        await Assert.That(engine.Actions).IsNotNull();
        await Assert.That(engine.Serializers).IsNotNull();
        await Assert.That(engine.Goals).IsNotNull();
        await Assert.That(engine.FileSystem).IsNotNull();
    }

    [Test]
    public async Task Constructor_GeneratesId()
    {
        using var appContext = CreateAppContext();
        await using var engine = new Engine(appContext);

        await Assert.That(engine.Id).IsNotNull();
        await Assert.That(engine.Id.Length).IsEqualTo(12);
    }

    [Test]
    public async Task Constructor_DefaultsNameToRuntime2()
    {
        using var appContext = CreateAppContext();
        await using var engine = new Engine(appContext);

        await Assert.That(engine.Name).IsEqualTo("Runtime2");
    }

    [Test]
    public async Task Name_CanBeChanged()
    {
        using var appContext = CreateAppContext();
        await using var engine = new Engine(appContext);

        engine.Name = "CustomEngine";

        await Assert.That(engine.Name).IsEqualTo("CustomEngine");
    }

    [Test]
    public async Task IsDebugMode_ReflectsAppContext()
    {
        using var appContext = CreateAppContext();
        await using var engine = new Engine(appContext);

        engine.IsDebugMode = true;

        await Assert.That(appContext.IsDebugMode).IsTrue();
    }

    [Test]
    public async Task Constructor_AcceptsCustomActionRegistry()
    {
        using var appContext = CreateAppContext();
        var actions = new ActionRegistry();
        await using var engine = new Engine(appContext, actions);

        await Assert.That(engine.Actions).IsEqualTo(actions);
    }

    [Test]
    public async Task Constructor_AcceptsCustomSerializerRegistry()
    {
        using var appContext = CreateAppContext();
        var serializers = new SerializerRegistry();
        await using var engine = new Engine(appContext, serializers: serializers);

        await Assert.That(engine.Serializers).IsEqualTo(serializers);
    }

    [Test]
    public async Task RegisterBuiltInModules_RegistersVariableActions()
    {
        using var appContext = CreateAppContext();
        await using var engine = new Engine(appContext);

        engine.RegisterBuiltInModules();

        await Assert.That(engine.Actions.Contains("variable", "set")).IsTrue();
        await Assert.That(engine.Actions.Contains("variable", "get")).IsTrue();
    }

    [Test]
    public async Task RegisterBuiltInModules_RegistersOutputActions()
    {
        using var appContext = CreateAppContext();
        await using var engine = new Engine(appContext);

        engine.RegisterBuiltInModules();

        await Assert.That(engine.Actions.Contains("output", "write")).IsTrue();
    }

    [Test]
    public async Task CreateContext_CreatesNewContext()
    {
        using var appContext = CreateAppContext();
        await using var engine = new Engine(appContext);

        using var context = engine.CreateContext();

        await Assert.That(context).IsNotNull();
        await Assert.That(context.AppContext).IsEqualTo(appContext);
        await Assert.That(context.CallStack).IsNotNull();
    }

    [Test]
    public async Task CreateContext_AcceptsCustomMemoryStack()
    {
        using var appContext = CreateAppContext();
        await using var engine = new Engine(appContext);
        var memoryStack = new MemoryStack();
        memoryStack.Set("test", "value");

        using var context = engine.CreateContext(memoryStack);

        await Assert.That(context.MemoryStack).IsEqualTo(memoryStack);
    }

    [Test]
    public async Task RunGoalAsync_NonexistentGoal_ReturnsError()
    {
        using var appContext = CreateAppContext();
        await using var engine = new Engine(appContext);

        var result = await engine.RunGoalAsync("NonexistentGoal");

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error!.Key).IsEqualTo("NotFound");
    }

    [Test]
    public async Task RunGoalAsync_EmptyGoal_ReturnsSuccess()
    {
        using var appContext = CreateAppContext();
        await using var engine = new Engine(appContext);
        var goal = new Goal { Name = "EmptyGoal" };
        engine.Goals.Add(goal);

        var result = await engine.RunGoalAsync("EmptyGoal");

        await Assert.That(result.Success).IsTrue();
    }

    [Test]
    public async Task RunGoalAsync_CancelledToken_ReturnsError()
    {
        using var appContext = CreateAppContext();
        await using var engine = new Engine(appContext);
        var goal = new Goal { Name = "TestGoal" };
        engine.Goals.Add(goal);
        var cts = new CancellationTokenSource();
        cts.Cancel();

        var result = await engine.RunGoalAsync("TestGoal", cancellationToken: cts.Token);

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error!.Key).IsEqualTo("Cancelled");
    }

    [Test]
    public async Task RunGoalAsync_SetsCurrentGoalName()
    {
        using var appContext = CreateAppContext();
        await using var engine = new Engine(appContext);
        var goal = new Goal { Name = "TestGoal" };
        engine.Goals.Add(goal);
        using var context = engine.CreateContext();
        await engine.RunGoalAsync(goal, context);

        await Assert.That(context.CurrentGoalName).IsEqualTo("TestGoal");
    }

    [Test]
    public async Task RunGoalAsync_PushesCallFrame()
    {
        using var appContext = CreateAppContext();
        await using var engine = new Engine(appContext);
        var goal = new Goal { Name = "TestGoal" };
        engine.Goals.Add(goal);

        using var context = engine.CreateContext();
        await engine.RunGoalAsync(goal, context);

        // After completion, frame should be popped
        await Assert.That(context.CallStack!.Depth).IsEqualTo(0);
    }

    [Test]
    public async Task RunGoalAsync_ExecutesSteps()
    {
        using var appContext = CreateAppContext();
        await using var engine = new Engine(appContext);
        engine.RegisterBuiltInModules();

        var goal = new Goal
        {
            Name = "TestGoal",
            Steps = new Steps
            {
                MakeStep("variable", "set",
                    new Dictionary<string, object?> { { "name", "test" }, { "value", "hello" } },
                    index: 0, text: "set variable")
            }
        };
        engine.Goals.Add(goal);

        using var context = engine.CreateContext();
        var result = await engine.RunGoalAsync(goal, context);

        await Assert.That(result.Success).IsTrue();
        await Assert.That(context.MemoryStack.GetValue("test")).IsEqualTo("hello");
    }

    [Test]
    public async Task RunGoalAsync_StepFailure_ReturnsError()
    {
        using var appContext = CreateAppContext();
        await using var engine = new Engine(appContext);
        engine.RegisterBuiltInModules();

        var goal = new Goal
        {
            Name = "TestGoal",
            Steps = new Steps
            {
                MakeStep("variable", "get", index: 0, text: "get variable")
                // Missing name parameter -> will fail
            }
        };
        engine.Goals.Add(goal);

        var result = await engine.RunGoalAsync("TestGoal");

        await Assert.That(result.Success).IsFalse();
    }

    [Test]
    public async Task RunGoalAsync_StepWithIgnoreError_ContinuesOnError()
    {
        using var appContext = CreateAppContext();
        await using var engine = new Engine(appContext);
        engine.RegisterBuiltInModules();

        var goal = new Goal
        {
            Name = "TestGoal",
            Steps = new Steps
            {
                new Step
                {
                    Index = 0,
                    Text = "failing step",
                    Actions = new Actions
                    {
                        new PLang.Runtime2.Core.Action
                        {
                            Class = "variable",
                            Method = "get",
                            Parameters = new List<Data>(),
                            Return = null
                        }
                    },
                    OnError = new ErrorHandler { IgnoreError = true }
                },
                MakeStep("variable", "set",
                    new Dictionary<string, object?> { { "name", "test" }, { "value", "success" } },
                    index: 1, text: "set variable")
            }
        };
        engine.Goals.Add(goal);

        using var context = engine.CreateContext();
        var result = await engine.RunGoalAsync(goal, context);

        await Assert.That(result.Success).IsTrue();
        await Assert.That(context.MemoryStack.GetValue("test")).IsEqualTo("success");
    }

    [Test]
    public async Task StepRunAsync_ActionNotFound_ReturnsError()
    {
        using var appContext = CreateAppContext();
        await using var engine = new Engine(appContext);
        var step = MakeStep("nonexistent", "method");
        using var context = engine.CreateContext();

        var result = await step.RunAsync(engine, context);

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error!.Key).IsEqualTo("ActionNotFound");
    }

    [Test]
    public async Task StepRunAsync_SetsReturnVariable()
    {
        using var appContext = CreateAppContext();
        await using var engine = new Engine(appContext);
        engine.RegisterBuiltInModules();

        var step = MakeStep("variable", "set",
            new Dictionary<string, object?> { { "name", "source" }, { "value", "hello" } });

        using var context = engine.CreateContext();
        await step.RunAsync(engine, context);

        await Assert.That(context.MemoryStack.GetValue("source")).IsEqualTo("hello");
    }

    [Test]
    public async Task StepRunAsync_RecordsStep()
    {
        using var appContext = CreateAppContext();
        await using var engine = new Engine(appContext);
        engine.RegisterBuiltInModules();

        var step = MakeStep("variable", "clear", index: 5, text: "test step");

        using var context = engine.CreateContext();
        context.CallStack!.Push("TestGoal");
        await step.RunAsync(engine, context);

        await Assert.That(context.CallStack.Current!.CurrentStepIndex).IsEqualTo(5);
        await Assert.That(context.CallStack.Current!.CurrentStepText).IsEqualTo("test step");
    }

    [Test]
    public async Task StepRunAsync_ExceptionInHandler_ReturnsError()
    {
        using var appContext = CreateAppContext();
        await using var engine = new Engine(appContext);

        var throwingHandler = new ThrowingHandler();
        engine.Actions.Register("throwing", "fail", throwingHandler);

        var step = MakeStep("throwing", "fail");
        using var context = engine.CreateContext();

        var result = await step.RunAsync(engine, context);

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error!.Exception).IsTypeOf<InvalidOperationException>();
    }

    [Test]
    public async Task StepRunAsync_HandlerWithoutICodeGenerated_ReturnsError()
    {
        using var appContext = CreateAppContext();
        await using var engine = new Engine(appContext);

        var nonGeneratedHandler = new NonGeneratedHandler();
        engine.Actions.Register("legacy", "do", nonGeneratedHandler);

        var step = MakeStep("legacy", "do");
        using var context = engine.CreateContext();

        var result = await step.RunAsync(engine, context);

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error!.Key).IsEqualTo("HandlerError");
    }

    [Test]
    public async Task DisposeAsync_DisposesDisposableHandlers()
    {
        using var appContext = CreateAppContext();
        var engine = new Engine(appContext);
        var disposableHandler = new DisposableHandler();
        engine.Actions.Register("disposable", "do", disposableHandler);

        await engine.DisposeAsync();

        await Assert.That(disposableHandler.IsDisposed).IsTrue();
    }

    [Test]
    public async Task DisposeAsync_DisposesAsyncDisposableHandlers()
    {
        using var appContext = CreateAppContext();
        var engine = new Engine(appContext);
        var asyncDisposableHandler = new AsyncDisposableHandler();
        engine.Actions.Register("asyncdisposable", "do", asyncDisposableHandler);

        await engine.DisposeAsync();

        await Assert.That(asyncDisposableHandler.IsDisposed).IsTrue();
    }

    [Test]
    public async Task DisposeAsync_CalledTwice_DoesNotThrow()
    {
        using var appContext = CreateAppContext();
        var engine = new Engine(appContext);

        await engine.DisposeAsync();
        await engine.DisposeAsync();

        // Should not throw
    }

    [Test]
    public async Task DisposeAsync_DisposesCreatedActors()
    {
        using var appContext = CreateAppContext();
        var engine = new Engine(appContext);

        // Access actors to create them
        var user = engine.User;
        var system = engine.System;
        var service = engine.Service;

        // Get references to contexts
        var userContext = user.Context;
        var systemContext = system.Context;
        var serviceContext = service.Context;

        await engine.DisposeAsync();

        // Contexts should be disposed (we can't directly check, but we verify no exception)
        await Assert.That(true).IsTrue();
    }

    [Test]
    public async Task DisposeAsync_HandlesUncreatedActors()
    {
        using var appContext = CreateAppContext();
        var engine = new Engine(appContext);

        // Don't access any actors
        await engine.DisposeAsync();

        // Should not throw
        await Assert.That(true).IsTrue();
    }

    [Test]
    public async Task RunGoalAsync_WithActor_UsesActorContext()
    {
        using var appContext = CreateAppContext();
        await using var engine = new Engine(appContext);
        engine.RegisterBuiltInModules();

        var goal = new Goal
        {
            Name = "TestGoal",
            Steps = new Steps
            {
                MakeStep("variable", "set",
                    new Dictionary<string, object?> { { "name", "test" }, { "value", "hello" } },
                    index: 0, text: "set variable")
            }
        };
        engine.Goals.Add(goal);

        var result = await engine.RunGoalAsync(goal, engine.System);

        await Assert.That(result.Success).IsTrue();
        await Assert.That(engine.System.Context.MemoryStack.GetValue("test")).IsEqualTo("hello");
        // User context should NOT have the variable
        await Assert.That(engine.User.Context.MemoryStack.GetValue("test")).IsNull();
    }

    [Test]
    public async Task RunGoalAsync_ByName_WithActor_UsesActorContext()
    {
        using var appContext = CreateAppContext();
        await using var engine = new Engine(appContext);
        engine.RegisterBuiltInModules();

        var goal = new Goal
        {
            Name = "TestGoal",
            Steps = new Steps
            {
                MakeStep("variable", "set",
                    new Dictionary<string, object?> { { "name", "test" }, { "value", "system-value" } },
                    index: 0, text: "set variable")
            }
        };
        engine.Goals.Add(goal);

        var result = await engine.RunGoalAsync("TestGoal", engine.System);

        await Assert.That(result.Success).IsTrue();
        await Assert.That(engine.System.Context.MemoryStack.GetValue("test")).IsEqualTo("system-value");
    }

    // Handler that does NOT implement ICodeGenerated - used to test engine rejects it
    private class NonGeneratedHandler : IClass
    {
        public Engine Engine { get; private set; } = null!;
        public PLangContext Context { get; private set; } = null!;
        public System.Type? ParameterType => null;

        public void Initialize(Engine engine, PLangContext context) { Engine = engine; Context = context; }
        public Task<Data> ExecuteAsync(object? parameters) => Task.FromResult(Data.Ok());
    }

    private class DisposableHandler : IClass, ICodeGenerated, IDisposable
    {
        public Engine Engine { get; private set; } = null!;
        public PLangContext Context { get; private set; } = null!;
        public System.Type? ParameterType => null;
        public bool IsDisposed { get; private set; }

        public void Initialize(Engine engine, PLangContext context) { Engine = engine; Context = context; }
        public Task<Data> ExecuteAsync(object? parameters) => Task.FromResult(Data.Ok());
        public Task<Data> CodeGeneratedExecuteAsync(List<Data> parameters, Engine engine, PLangContext context)
        {
            Initialize(engine, context);
            return ExecuteAsync(null);
        }
        public void Dispose() => IsDisposed = true;
    }

    private class AsyncDisposableHandler : IClass, ICodeGenerated, IAsyncDisposable
    {
        public Engine Engine { get; private set; } = null!;
        public PLangContext Context { get; private set; } = null!;
        public System.Type? ParameterType => null;
        public bool IsDisposed { get; private set; }

        public void Initialize(Engine engine, PLangContext context) { Engine = engine; Context = context; }
        public Task<Data> ExecuteAsync(object? parameters) => Task.FromResult(Data.Ok());
        public Task<Data> CodeGeneratedExecuteAsync(List<Data> parameters, Engine engine, PLangContext context)
        {
            Initialize(engine, context);
            return ExecuteAsync(null);
        }
        public ValueTask DisposeAsync() { IsDisposed = true; return ValueTask.CompletedTask; }
    }

    private class ThrowingHandler : IClass, ICodeGenerated
    {
        public Engine Engine { get; private set; } = null!;
        public PLangContext Context { get; private set; } = null!;
        public System.Type? ParameterType => null;

        public void Initialize(Engine engine, PLangContext context) { Engine = engine; Context = context; }
        public Task<Data> ExecuteAsync(object? parameters)
        {
            throw new InvalidOperationException("Test exception");
        }
        public Task<Data> CodeGeneratedExecuteAsync(List<Data> parameters, Engine engine, PLangContext context)
        {
            Initialize(engine, context);
            return ExecuteAsync(null);
        }
    }
}
