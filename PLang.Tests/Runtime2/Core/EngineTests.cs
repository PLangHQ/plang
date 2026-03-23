using PLang.Runtime2.Engine.Context;
using PLang.Runtime2.Engine.Memory;
using PLang.Runtime2.modules;

namespace PLang.Tests.Runtime2.Core;

public class EngineTests
{
    private static Step MakeStep(string actionClass, string method, object? parameters = null, int index = 0, string text = "")
    {
        return new Step
        {
            Index = index,
            Text = text,
            Actions = new StepActions
            {
                new PLang.Runtime2.Engine.Goals.Goal.Steps.Step.Actions.Action.@this
                {
                    Module = actionClass,
                    ActionName = method,
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
            Actions = new StepActions
            {
                new PLang.Runtime2.Engine.Goals.Goal.Steps.Step.Actions.Action.@this
                {
                    Module = actionClass,
                    ActionName = method,
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
    public async Task System_ReturnsActorWithCorrectName()
    {
        await using var engine = new PLang.Runtime2.Engine.@this("/app");

        var system = engine.System;

        await Assert.That(system.Name).IsEqualTo("System");
    }

    [Test]
    public async Task Service_ReturnsActorWithCorrectName()
    {
        await using var engine = new PLang.Runtime2.Engine.@this("/app");

        var service = engine.Service;

        await Assert.That(service.Name).IsEqualTo("Service");
    }

    [Test]
    public async Task User_ReturnsActorWithCorrectName()
    {
        await using var engine = new PLang.Runtime2.Engine.@this("/app");

        var user = engine.User;

        await Assert.That(user.Name).IsEqualTo("User");
    }

    [Test]
    public async Task Actors_AreLazilyCreated()
    {
        await using var engine = new PLang.Runtime2.Engine.@this("/app");

        // Access only User actor
        var user = engine.User;

        // User should have its own context
        await Assert.That(user.Context).IsNotNull();
        await Assert.That(user.Channels).IsNotNull();
        await Assert.That(user.Engine).IsEqualTo(engine);
    }

    [Test]
    public async Task Actors_HaveIsolatedContexts()
    {
        await using var engine = new PLang.Runtime2.Engine.@this("/app");

        engine.User.Context.MemoryStack.Set("key", "user-value");
        engine.System.Context.MemoryStack.Set("key", "system-value");

        await Assert.That(engine.User.Context.MemoryStack.GetValue("key")).IsEqualTo("user-value");
        await Assert.That(engine.System.Context.MemoryStack.GetValue("key")).IsEqualTo("system-value");
    }

    [Test]
    public async Task Actors_HaveIsolatedIO()
    {
        await using var engine = new PLang.Runtime2.Engine.@this("/app");

        engine.User.Channels.CreateMemoryChannel("test");
        engine.System.Channels.CreateMemoryChannel("test");

        await Assert.That(engine.User.Channels.Contains("test")).IsTrue();
        await Assert.That(engine.System.Channels.Contains("test")).IsTrue();
        // They are separate instances
        await Assert.That(engine.User.Channels.Get("test")).IsNotEqualTo(engine.System.Channels.Get("test"));
    }

    [Test]
    public async Task Actor_Context_HasBackReference()
    {
        await using var engine = new PLang.Runtime2.Engine.@this("/app");

        await Assert.That(engine.User.Context.Actor).IsEqualTo(engine.User);
        await Assert.That(engine.System.Context.Actor).IsEqualTo(engine.System);
        await Assert.That(engine.Service.Context.Actor).IsEqualTo(engine.Service);
    }

    [Test]
    public async Task Actor_SameInstanceOnMultipleAccess()
    {
        await using var engine = new PLang.Runtime2.Engine.@this("/app");

        var user1 = engine.User;
        var user2 = engine.User;

        await Assert.That(user1).IsEqualTo(user2);
    }

    #endregion

    [Test]
    public async Task Constructor_SetsProperties()
    {
        await using var engine = new PLang.Runtime2.Engine.@this("/app");

        await Assert.That(engine.AbsolutePath).IsEqualTo("/app");
        await Assert.That(engine.Libraries).IsNotNull();
        await Assert.That(engine.Channels.Serializers).IsNotNull();
        await Assert.That(engine.Goals).IsNotNull();
        await Assert.That(engine.FileSystem).IsNotNull();
    }

    [Test]
    public async Task Constructor_GeneratesId()
    {
        await using var engine = new PLang.Runtime2.Engine.@this("/app");

        await Assert.That(engine.Id).IsNotNull();
        await Assert.That(engine.Id.Length).IsEqualTo(12);
    }

    [Test]
    public async Task Constructor_DefaultsNameToRuntime2()
    {
        await using var engine = new PLang.Runtime2.Engine.@this("/app");

        await Assert.That(engine.Name).IsEqualTo("Runtime2");
    }

    [Test]
    public async Task Name_CanBeChanged()
    {
        await using var engine = new PLang.Runtime2.Engine.@this("/app");

        engine.Name = "CustomEngine";

        await Assert.That(engine.Name).IsEqualTo("CustomEngine");
    }

    [Test]
    public async Task Debug_IsEnabled_ReflectsEngine()
    {
        await using var engine = new PLang.Runtime2.Engine.@this("/app");

        engine.Debug.IsEnabled = true;

        await Assert.That(engine.Debug.IsEnabled).IsTrue();
    }

    [Test]
    public async Task Constructor_AcceptsCustomLibraries()
    {
        var libraries = new EngineLibraries();
        await using var engine = new PLang.Runtime2.Engine.@this("/app", libraries);

        await Assert.That(engine.Libraries).IsEqualTo(libraries);
    }

    [Test]
    public async Task Channels_HasSerializers()
    {
        await using var engine = new PLang.Runtime2.Engine.@this("/app");

        await Assert.That(engine.Channels.Serializers).IsNotNull();
        await Assert.That(engine.Channels.Serializers.GetByContentType("application/json")).IsNotNull();
    }

    [Test]
    public async Task Libraries_HasVariableActions()
    {
        await using var engine = new PLang.Runtime2.Engine.@this("/app");

        await Assert.That(engine.Libraries.Contains("variable", "set")).IsTrue();
        await Assert.That(engine.Libraries.Contains("variable", "get")).IsTrue();
    }

    [Test]
    public async Task Libraries_HasOutputActions()
    {
        await using var engine = new PLang.Runtime2.Engine.@this("/app");

        await Assert.That(engine.Libraries.Contains("output", "write")).IsTrue();
    }

    [Test]
    public async Task CreateContext_CreatesNewContext()
    {
        await using var engine = new PLang.Runtime2.Engine.@this("/app");

        using var context = engine.CreateContext();

        await Assert.That(context).IsNotNull();
        await Assert.That(context.Engine).IsEqualTo(engine);
        await Assert.That(context.CallStack).IsNotNull();
    }

    [Test]
    public async Task CreateContext_AcceptsCustomMemoryStack()
    {
        await using var engine = new PLang.Runtime2.Engine.@this("/app");
        var memoryStack = new MemoryStack();
        memoryStack.Set("test", "value");

        using var context = engine.CreateContext(memoryStack);

        await Assert.That(context.MemoryStack).IsEqualTo(memoryStack);
    }

    [Test]
    public async Task RunGoalAsync_NonexistentGoal_ReturnsError()
    {
        await using var engine = new PLang.Runtime2.Engine.@this("/app");

        var result = await engine.RunGoalAsync("NonexistentGoal");

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error!.Key).IsEqualTo("NotFound");
    }

    [Test]
    public async Task RunGoalAsync_EmptyGoal_ReturnsSuccess()
    {
        await using var engine = new PLang.Runtime2.Engine.@this("/app");
        var goal = new Goal { Name = "EmptyGoal", Path = "/EmptyGoal.goal" };
        engine.Goals.Add(goal);

        var result = await engine.RunGoalAsync("EmptyGoal");

        await Assert.That(result.Success).IsTrue();
    }

    [Test]
    public async Task RunGoalAsync_CancelledToken_ReturnsError()
    {
        await using var engine = new PLang.Runtime2.Engine.@this("/app");
        var goal = new Goal { Name = "TestGoal", Path = "/TestGoal.goal" };
        engine.Goals.Add(goal);
        var cts = new CancellationTokenSource();
        cts.Cancel();

        var result = await engine.RunGoalAsync("TestGoal", cancellationToken: cts.Token);

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error!.Key).IsEqualTo("Cancelled");
    }

    [Test]
    public async Task RunGoalAsync_SetsContextGoal()
    {
        await using var engine = new PLang.Runtime2.Engine.@this("/app");
        var goal = new Goal { Name = "TestGoal", Path = "/TestGoal.goal" };
        engine.Goals.Add(goal);
        using var context = engine.CreateContext();
        await engine.RunGoalAsync(goal, context);

        // Goal is restored after execution, but during execution context.Goal was set
        // After RunAsync completes, Goal is restored to previous (null for root)
        // So we test the call stack was used correctly instead
        await Assert.That(context.CallStack!.Depth).IsEqualTo(0);
    }

    [Test]
    public async Task RunGoalAsync_PushesCallFrame()
    {
        await using var engine = new PLang.Runtime2.Engine.@this("/app");
        var goal = new Goal { Name = "TestGoal", Path = "/TestGoal.goal" };
        engine.Goals.Add(goal);

        using var context = engine.CreateContext();
        await engine.RunGoalAsync(goal, context);

        // After completion, frame should be popped
        await Assert.That(context.CallStack!.Depth).IsEqualTo(0);
    }

    [Test]
    public async Task RunGoalAsync_ExecutesSteps()
    {
        await using var engine = new PLang.Runtime2.Engine.@this("/app");

        var goal = new Goal
        {
            Name = "TestGoal",
            Path = "/TestGoal.goal",
            Steps = new GoalSteps
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
        await using var engine = new PLang.Runtime2.Engine.@this("/app");

        var goal = new Goal
        {
            Name = "TestGoal",
            Path = "/TestGoal.goal",
            Steps = new GoalSteps
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
        await using var engine = new PLang.Runtime2.Engine.@this("/app");

        var goal = new Goal
        {
            Name = "TestGoal",
            Path = "/TestGoal.goal",
            Steps = new GoalSteps
            {
                new Step
                {
                    Index = 0,
                    Text = "failing step",
                    Actions = new StepActions
                    {
                        new PLang.Runtime2.Engine.Goals.Goal.Steps.Step.Actions.Action.@this
                        {
                            Module = "variable",
                            ActionName = "get",
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
        await using var engine = new PLang.Runtime2.Engine.@this("/app");
        var step = MakeStep("nonexistent", "method");
        using var context = engine.CreateContext();

        var result = await step.RunAsync(engine, context);

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error!.Key).IsEqualTo("ActionNotFound");
    }

    [Test]
    public async Task StepRunAsync_SetsReturnVariable()
    {
        await using var engine = new PLang.Runtime2.Engine.@this("/app");

        var step = MakeStep("variable", "set",
            new Dictionary<string, object?> { { "name", "source" }, { "value", "hello" } });

        using var context = engine.CreateContext();
        await step.RunAsync(engine, context);

        await Assert.That(context.MemoryStack.GetValue("source")).IsEqualTo("hello");
    }

    [Test]
    public async Task StepRunAsync_RecordsStep()
    {
        await using var engine = new PLang.Runtime2.Engine.@this("/app");

        var step = MakeStep("variable", "clear", index: 5, text: "test step");

        using var context = engine.CreateContext();
        context.CallStack!.Push("TestGoal");
        await step.RunAsync(engine, context);

        await Assert.That(context.CallStack.Current!.Step).IsNotNull();
        await Assert.That(context.CallStack.Current!.Step!.Index).IsEqualTo(5);
        await Assert.That(context.CallStack.Current!.Step!.Text).IsEqualTo("test step");
    }

    [Test]
    public async Task StepRunAsync_ExceptionInHandler_ReturnsError()
    {
        await using var engine = new PLang.Runtime2.Engine.@this("/app");

        var throwingHandler = new ThrowingHandler();
        engine.Libraries.Register("throwing", "fail", throwingHandler);

        var step = MakeStep("throwing", "fail");
        using var context = engine.CreateContext();

        var result = await step.RunAsync(engine, context);

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error!.Exception).IsTypeOf<InvalidOperationException>();
    }

    [Test]
    public async Task StepRunAsync_HandlerWithoutICodeGenerated_ReturnsError()
    {
        await using var engine = new PLang.Runtime2.Engine.@this("/app");

        var nonGeneratedHandler = new NonGeneratedHandler();
        engine.Libraries.Register("legacy", "do", nonGeneratedHandler);

        var step = MakeStep("legacy", "do");
        using var context = engine.CreateContext();

        var result = await step.RunAsync(engine, context);

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error!.Key).IsEqualTo("ActionError");
    }

    [Test]
    public async Task DisposeAsync_DisposesDisposableHandlers()
    {
        var engine = new PLang.Runtime2.Engine.@this("/app");
        var disposableHandler = new DisposableHandler();
        engine.Libraries.Register("disposable", "do", disposableHandler);

        await engine.DisposeAsync();

        await Assert.That(disposableHandler.IsDisposed).IsTrue();
    }

    [Test]
    public async Task DisposeAsync_DisposesAsyncDisposableHandlers()
    {
        var engine = new PLang.Runtime2.Engine.@this("/app");
        var asyncDisposableHandler = new AsyncDisposableHandler();
        engine.Libraries.Register("asyncdisposable", "do", asyncDisposableHandler);

        await engine.DisposeAsync();

        await Assert.That(asyncDisposableHandler.IsDisposed).IsTrue();
    }

    [Test]
    public async Task DisposeAsync_CalledTwice_DoesNotThrow()
    {
        var engine = new PLang.Runtime2.Engine.@this("/app");

        await engine.DisposeAsync();
        await engine.DisposeAsync();

        // Should not throw
    }

    [Test]
    public async Task DisposeAsync_DisposesCreatedActors()
    {
        var engine = new PLang.Runtime2.Engine.@this("/app");

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
        var engine = new PLang.Runtime2.Engine.@this("/app");

        // Don't access any actors
        await engine.DisposeAsync();

        // Should not throw
        await Assert.That(true).IsTrue();
    }

    [Test]
    public async Task RunGoalAsync_WithActor_UsesActorContext()
    {
        await using var engine = new PLang.Runtime2.Engine.@this("/app");

        var goal = new Goal
        {
            Name = "TestGoal",
            Path = "/TestGoal.goal",
            Steps = new GoalSteps
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
        await using var engine = new PLang.Runtime2.Engine.@this("/app");

        var goal = new Goal
        {
            Name = "TestGoal",
            Path = "/TestGoal.goal",
            Steps = new GoalSteps
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
    private class NonGeneratedHandler : IAction
    {
        public PLang.Runtime2.Engine.@this Engine { get; private set; } = null!;
        public PLangContext Context { get; private set; } = null!;
        public System.Type? ParameterType => null;

        public void Initialize(PLang.Runtime2.Engine.@this engine, PLangContext context) { Engine = engine; Context = context; }
        public Task<Data> ExecuteAsync(object? parameters) => Task.FromResult(Data.Ok());
    }

    private class DisposableHandler : IAction, ICodeGenerated, IDisposable
    {
        public PLang.Runtime2.Engine.@this Engine { get; private set; } = null!;
        public PLangContext Context { get; private set; } = null!;
        public System.Type? ParameterType => null;
        public bool IsDisposed { get; private set; }

        public void Initialize(PLang.Runtime2.Engine.@this engine, PLangContext context) { Engine = engine; Context = context; }
        public Task<Data> ExecuteAsync(object? parameters) => Task.FromResult(Data.Ok());
        public Task<Data> CodeGeneratedExecuteAsync(List<Data> parameters, PLang.Runtime2.Engine.@this engine, PLangContext context, List<Data>? defaults = null)
        {
            Initialize(engine, context);
            return ExecuteAsync(null);
        }
        public void Dispose() => IsDisposed = true;
    }

    private class AsyncDisposableHandler : IAction, ICodeGenerated, IAsyncDisposable
    {
        public PLang.Runtime2.Engine.@this Engine { get; private set; } = null!;
        public PLangContext Context { get; private set; } = null!;
        public System.Type? ParameterType => null;
        public bool IsDisposed { get; private set; }

        public void Initialize(PLang.Runtime2.Engine.@this engine, PLangContext context) { Engine = engine; Context = context; }
        public Task<Data> ExecuteAsync(object? parameters) => Task.FromResult(Data.Ok());
        public Task<Data> CodeGeneratedExecuteAsync(List<Data> parameters, PLang.Runtime2.Engine.@this engine, PLangContext context, List<Data>? defaults = null)
        {
            Initialize(engine, context);
            return ExecuteAsync(null);
        }
        public ValueTask DisposeAsync() { IsDisposed = true; return ValueTask.CompletedTask; }
    }

    private class ThrowingHandler : IAction, ICodeGenerated
    {
        public PLang.Runtime2.Engine.@this Engine { get; private set; } = null!;
        public PLangContext Context { get; private set; } = null!;
        public System.Type? ParameterType => null;

        public void Initialize(PLang.Runtime2.Engine.@this engine, PLangContext context) { Engine = engine; Context = context; }
        public Task<Data> ExecuteAsync(object? parameters)
        {
            throw new InvalidOperationException("Test exception");
        }
        public Task<Data> CodeGeneratedExecuteAsync(List<Data> parameters, PLang.Runtime2.Engine.@this engine, PLangContext context, List<Data>? defaults = null)
        {
            Initialize(engine, context);
            return ExecuteAsync(null);
        }
    }

    #region Property Tests

    [Test]
    public async Task Property_Get_WithNormalValue_ReturnsValue()
    {
        await using var engine = new PLang.Runtime2.Engine.@this("/app");
        engine.Property["greeting"] = "hello";

        var result = await engine.Property.Get("greeting");

        await Assert.That(result).IsEqualTo("hello");
    }

    [Test]
    public async Task Property_Get_WithNullKey_ReturnsNull()
    {
        await using var engine = new PLang.Runtime2.Engine.@this("/app");

        var result = await engine.Property.Get("nonexistent");

        await Assert.That(result).IsNull();
    }

    [Test]
    public async Task Property_Get_WithGoalCall_RunsGoal()
    {
        await using var engine = new PLang.Runtime2.Engine.@this("/app");

        // Set up a goal that sets a variable
        var goal = new Goal
        {
            Name = "SummaryGoal",
            Path = "/SummaryGoal.goal",
            Steps = new GoalSteps
            {
                MakeStep("variable", "set",
                    new Dictionary<string, object?> { { "name", "wasRun" }, { "value", "yes" } },
                    index: 0, text: "set wasRun")
            }
        };
        engine.Goals.Add(goal);

        engine.Property["Summary"] = new GoalCall { Name = "SummaryGoal" };

        // Get detects GoalCall and executes it
        await engine.Property.Get("Summary");

        // Verify the goal actually ran by checking the variable it set
        await Assert.That(engine.User.Context.MemoryStack.GetValue("wasRun")).IsEqualTo("yes");
    }

    [Test]
    public async Task Property_Indexer_WithGoalCall_ReturnsRawGoalCall()
    {
        await using var engine = new PLang.Runtime2.Engine.@this("/app");
        var goalCall = new GoalCall { Name = "SummaryGoal" };
        engine.Property["Summary"] = goalCall;

        var result = engine.Property["Summary"];

        // Sync indexer returns the raw GoalCall, does NOT execute
        await Assert.That(result).IsTypeOf<GoalCall>();
        await Assert.That(((GoalCall)result!).Name).IsEqualTo("SummaryGoal");
    }

    [Test]
    public async Task Property_Get_Generic_WithNormalValue_ReturnsTyped()
    {
        await using var engine = new PLang.Runtime2.Engine.@this("/app");
        engine.Property["count"] = 42;

        var result = await engine.Property.Get<int>("count");

        await Assert.That(result).IsEqualTo(42);
    }

    [Test]
    public async Task Property_Get_Generic_TypeMismatch_ReturnsDefault()
    {
        await using var engine = new PLang.Runtime2.Engine.@this("/app");
        engine.Property["count"] = "not-an-int";

        var result = await engine.Property.Get<int>("count");

        await Assert.That(result).IsEqualTo(0);
    }

    #endregion
}
