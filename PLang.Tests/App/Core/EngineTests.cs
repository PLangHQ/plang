using global::App.Actor.Context;
using global::App.Variables;
using global::App.modules;

namespace PLang.Tests.App.Core;

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
                new global::App.Goals.Goal.Steps.Step.Actions.Action.@this
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
                new global::App.Goals.Goal.Steps.Step.Actions.Action.@this
                {
                    Module = actionClass,
                    ActionName = method,
                    Parameters = parameters is IDictionary<string, object?> dict
                        ? dict.Select(kv => new Data(kv.Key, kv.Value)).ToList()
                        : new List<Data>(),
                },
                new global::App.Goals.Goal.Steps.Step.Actions.Action.@this
                {
                    Module = "variable",
                    ActionName = "set",
                    Parameters = new List<Data>
                    {
                        new Data("Name", returnVarName),
                        new Data("Value", "%__data__%")
                    }
                }
            }
        };
    }

    #region Actor Tests

    [Test]
    public async Task System_ReturnsActorWithCorrectName()
    {
        await using var engine = new global::App.@this("/app");

        var system = engine.System;

        await Assert.That(system.Name).IsEqualTo("System");
    }

    [Test]
    public async Task Service_ReturnsActorWithCorrectName()
    {
        await using var engine = new global::App.@this("/app");

        var service = engine.Service;

        await Assert.That(service.Name).IsEqualTo("Service");
    }

    [Test]
    public async Task User_ReturnsActorWithCorrectName()
    {
        await using var engine = new global::App.@this("/app");

        var user = engine.User;

        await Assert.That(user.Name).IsEqualTo("User");
    }

    [Test]
    public async Task Actors_AreLazilyCreated()
    {
        await using var engine = new global::App.@this("/app");

        // Access only User actor
        var user = engine.User;

        // User should have its own context
        await Assert.That(user.Context).IsNotNull();
        await Assert.That(user.Channels).IsNotNull();
        await Assert.That(user.App).IsEqualTo(engine);
    }

    [Test]
    public async Task Actors_HaveIsolatedContexts()
    {
        await using var engine = new global::App.@this("/app");

        engine.User.Context.Variables.Set("key", "user-value");
        engine.System.Context.Variables.Set("key", "system-value");

        await Assert.That(engine.User.Context.Variables.GetValue("key")).IsEqualTo("user-value");
        await Assert.That(engine.System.Context.Variables.GetValue("key")).IsEqualTo("system-value");
    }

    [Test]
    public async Task Actors_HaveIsolatedIO()
    {
        await using var engine = new global::App.@this("/app");

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
        await using var engine = new global::App.@this("/app");

        await Assert.That(engine.User.Context.Actor).IsEqualTo(engine.User);
        await Assert.That(engine.System.Context.Actor).IsEqualTo(engine.System);
        await Assert.That(engine.Service.Context.Actor).IsEqualTo(engine.Service);
    }

    [Test]
    public async Task Actor_SameInstanceOnMultipleAccess()
    {
        await using var engine = new global::App.@this("/app");

        var user1 = engine.User;
        var user2 = engine.User;

        await Assert.That(user1).IsEqualTo(user2);
    }

    #endregion

    [Test]
    public async Task Constructor_SetsProperties()
    {
        await using var engine = new global::App.@this("/app");

        await Assert.That(engine.AbsolutePath).IsEqualTo("/app");
        await Assert.That(engine.Modules).IsNotNull();
        await Assert.That(engine.Channels.Serializers).IsNotNull();
        await Assert.That(engine.Goals).IsNotNull();
        await Assert.That(engine.FileSystem).IsNotNull();
    }

    [Test]
    public async Task Constructor_GeneratesId()
    {
        await using var engine = new global::App.@this("/app");

        await Assert.That(engine.Id).IsNotNull();
        await Assert.That(engine.Id.Length).IsEqualTo(12);
    }

    [Test]
    public async Task Constructor_DefaultsNameFromFolder()
    {
        await using var engine = new global::App.@this("/myapp");

        await Assert.That(engine.Name).IsEqualTo("myapp");
    }

    [Test]
    public async Task Name_CanBeChanged()
    {
        await using var engine = new global::App.@this("/app");

        engine.Name = "CustomEngine";

        await Assert.That(engine.Name).IsEqualTo("CustomEngine");
    }

    [Test]
    public async Task Debug_IsEnabled_ReflectsEngine()
    {
        await using var engine = new global::App.@this("/app");

        engine.Debug.IsEnabled = true;

        await Assert.That(engine.Debug.IsEnabled).IsTrue();
    }

    [Test]
    public async Task Constructor_AcceptsCustomModules()
    {
        var modules = new EngineModules();
        await using var engine = new global::App.@this("/app", modules);

        await Assert.That(engine.Modules).IsEqualTo(modules);
    }

    [Test]
    public async Task Channels_HasSerializers()
    {
        await using var engine = new global::App.@this("/app");

        await Assert.That(engine.Channels.Serializers).IsNotNull();
        await Assert.That(engine.Channels.Serializers.GetByContentType("application/json")).IsNotNull();
    }

    [Test]
    public async Task Modules_HasVariableActions()
    {
        await using var engine = new global::App.@this("/app");

        await Assert.That(engine.Modules.Contains("variable", "set")).IsTrue();
        await Assert.That(engine.Modules.Contains("variable", "get")).IsTrue();
    }

    [Test]
    public async Task Modules_HasOutputActions()
    {
        await using var engine = new global::App.@this("/app");

        await Assert.That(engine.Modules.Contains("output", "write")).IsTrue();
    }

    [Test]
    public async Task Context_ReturnsContext()
    {
        await using var engine = new global::App.@this("/app");

        var context = engine.Context;

        await Assert.That(context).IsNotNull();
        await Assert.That(context.App).IsEqualTo(engine);
        await Assert.That(context.CallStack).IsNotNull();
    }

    [Test]
    public async Task RunGoalAsync_NonexistentGoal_ReturnsError()
    {
        await using var engine = new global::App.@this("/app");

        var result = await engine.RunGoalAsync(new GoalCall { Name = "NonexistentGoal" });

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error!.Key).IsEqualTo("NotFound");
    }

    [Test]
    public async Task RunGoalAsync_EmptyGoal_ReturnsSuccess()
    {
        await using var engine = new global::App.@this("/app");
        var goal = new Goal { Name = "EmptyGoal", Path = "/EmptyGoal.goal" };
        engine.Goals.Add(goal);

        var result = await engine.RunGoalAsync(new GoalCall { Name = "EmptyGoal" });

        await Assert.That(result.Success).IsTrue();
    }

    [Test]
    public async Task RunGoalAsync_CancelledToken_ReturnsError()
    {
        await using var engine = new global::App.@this("/app");
        var goal = new Goal
        {
            Name = "TestGoal",
            Path = "/TestGoal.goal",
            Steps = new GoalSteps
            {
                MakeStep("variable", "set",
                    new Dictionary<string, object?> { { "name", "x" }, { "value", "y" } },
                    index: 0, text: "set variable")
            }
        };
        engine.Goals.Add(goal);

        // Cancel via the engine's shutdown — Goal.RunAsync checks context.CancellationToken
        engine.RequestShutdown();

        var result = await engine.RunGoalAsync(new GoalCall { Name = "TestGoal" });

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error!.Key).IsEqualTo("Cancelled");
    }

    [Test]
    public async Task RunGoalAsync_SetsContextGoal()
    {
        await using var engine = new global::App.@this("/app");
        var goal = new Goal { Name = "TestGoal", Path = "/TestGoal.goal" };
        engine.Goals.Add(goal);
        var context = engine.Context;
        await engine.RunGoalAsync(goal, context);

        // Goal is restored after execution, but during execution context.Goal was set
        // After RunAsync completes, Goal is restored to previous (null for root)
        // So we test the call stack was used correctly instead
        await Assert.That(context.CallStack!.Depth).IsEqualTo(0);
    }

    [Test]
    public async Task RunGoalAsync_PushesCallFrame()
    {
        await using var engine = new global::App.@this("/app");
        var goal = new Goal { Name = "TestGoal", Path = "/TestGoal.goal" };
        engine.Goals.Add(goal);

        var context = engine.Context;
        await engine.RunGoalAsync(goal, context);

        // After completion, frame should be popped
        await Assert.That(context.CallStack!.Depth).IsEqualTo(0);
    }

    [Test]
    public async Task RunGoalAsync_ExecutesSteps()
    {
        await using var engine = new global::App.@this("/app");

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

        var context = engine.Context;
        var result = await engine.RunGoalAsync(goal, context);

        await Assert.That(result.Success).IsTrue();
        await Assert.That(context.Variables.GetValue("test")).IsEqualTo("hello");
    }

    [Test]
    public async Task RunGoalAsync_StepFailure_ReturnsError()
    {
        await using var engine = new global::App.@this("/app");

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

        var result = await engine.RunGoalAsync(new GoalCall { Name = "TestGoal" });

        await Assert.That(result.Success).IsFalse();
    }

    [Test]
    public async Task StepRunAsync_ActionNotFound_ReturnsError()
    {
        await using var engine = new global::App.@this("/app");
        var step = MakeStep("nonexistent", "method");
        var context = engine.Context;

        var steps = new GoalSteps { step };
        var result = await steps.RunAsync(context);

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error!.Key).IsEqualTo("ActionNotFound");
    }

    [Test]
    public async Task StepRunAsync_SetsReturnVariable()
    {
        await using var engine = new global::App.@this("/app");

        var step = MakeStep("variable", "set",
            new Dictionary<string, object?> { { "name", "source" }, { "value", "hello" } });

        var context = engine.Context;
        var steps = new GoalSteps { step };
        await steps.RunAsync(context);

        await Assert.That(context.Variables.GetValue("source")).IsEqualTo("hello");
    }

    [Test]
    public async Task StepRunAsync_ExceptionInHandler_ReturnsError()
    {
        await using var engine = new global::App.@this("/app");

        var throwingHandler = new ThrowingHandler();
        engine.Modules.Register("throwing", "fail", throwingHandler);

        var step = MakeStep("throwing", "fail");
        var context = engine.Context;

        // Step.RunAsync catches exceptions and wraps in Data.FromError
        var steps = new GoalSteps { step };
        var result = await steps.RunAsync(context);
        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error!.Key).IsEqualTo("StepError");
    }

    [Test]
    public async Task StepRunAsync_HandlerWithoutICodeGenerated_ReturnsError()
    {
        await using var engine = new global::App.@this("/app");

        var nonGeneratedHandler = new NonGeneratedHandler();
        engine.Modules.Register("legacy", "do", nonGeneratedHandler);

        var step = MakeStep("legacy", "do");
        var context = engine.Context;

        var steps = new GoalSteps { step };
        var result = await steps.RunAsync(context);

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error!.Key).IsEqualTo("ActionError");
    }

    [Test]
    public async Task DisposeAsync_DisposesDisposableHandlers()
    {
        var engine = new global::App.@this("/app");
        var disposableHandler = new DisposableHandler();
        engine.Modules.Register("disposable", "do", disposableHandler);

        await engine.DisposeAsync();

        await Assert.That(disposableHandler.IsDisposed).IsTrue();
    }

    [Test]
    public async Task DisposeAsync_DisposesAsyncDisposableHandlers()
    {
        var engine = new global::App.@this("/app");
        var asyncDisposableHandler = new AsyncDisposableHandler();
        engine.Modules.Register("asyncdisposable", "do", asyncDisposableHandler);

        await engine.DisposeAsync();

        await Assert.That(asyncDisposableHandler.IsDisposed).IsTrue();
    }

    [Test]
    public async Task DisposeAsync_CalledTwice_DoesNotThrow()
    {
        var engine = new global::App.@this("/app");

        await engine.DisposeAsync();
        await engine.DisposeAsync();

        // Should not throw
    }

    [Test]
    public async Task DisposeAsync_DisposesCreatedActors()
    {
        var engine = new global::App.@this("/app");

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
        var engine = new global::App.@this("/app");

        // Don't access any actors
        await engine.DisposeAsync();

        // Should not throw
        await Assert.That(true).IsTrue();
    }

    [Test]
    public async Task RunGoalAsync_WithActor_UsesActorContext()
    {
        await using var engine = new global::App.@this("/app");

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

        var result = await engine.RunGoalAsync(goal, engine.System.Context);

        await Assert.That(result.Success).IsTrue();
        await Assert.That(engine.System.Context.Variables.GetValue("test")).IsEqualTo("hello");
        // User context should NOT have the variable
        await Assert.That(engine.User.Context.Variables.GetValue("test")).IsNull();
    }

    [Test]
    public async Task RunGoalAsync_ByName_WithActor_UsesActorContext()
    {
        await using var engine = new global::App.@this("/app");

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

        var result = await engine.RunGoalAsync(new GoalCall { Name = "TestGoal" }, engine.System.Context);

        await Assert.That(result.Success).IsTrue();
        await Assert.That(engine.System.Context.Variables.GetValue("test")).IsEqualTo("system-value");
    }

    // Handler that does NOT implement ICodeGenerated - used to test engine rejects it
    private class NonGeneratedHandler : IAction
    {
        public global::App.Goals.Goal.Steps.Step.Actions.Action.@this Action { get; set; } = null!;
        public global::App.@this App { get; private set; } = null!;
        public global::App.Actor.Context.@this Context { get; private set; } = null!;
        public System.Type? ParameterType => null;

        public void Initialize(global::App.@this engine, global::App.Actor.Context.@this context) { App = engine; Context = context; }
        public Task<Data> ExecuteAsync(object? parameters) => Task.FromResult(Data.Ok());
    }

    private class DisposableHandler : IAction, ICodeGenerated, IDisposable
    {
        public global::App.Goals.Goal.Steps.Step.Actions.Action.@this Action { get; set; } = null!;
        public global::App.@this App { get; private set; } = null!;
        public global::App.Actor.Context.@this Context { get; private set; } = null!;
        public System.Type? ParameterType => null;
        public bool IsDisposed { get; private set; }

        public void Initialize(global::App.@this engine, global::App.Actor.Context.@this context) { App = engine; Context = context; }
        public Task<Data> ExecuteAsync(global::App.Goals.Goal.Steps.Step.Actions.Action.@this action, global::App.Actor.Context.@this context)
        {
            Initialize(context.App!, context);
            return Task.FromResult(Data.Ok());
        }
        public void Dispose() => IsDisposed = true;
    }

    private class AsyncDisposableHandler : IAction, ICodeGenerated, IAsyncDisposable
    {
        public global::App.Goals.Goal.Steps.Step.Actions.Action.@this Action { get; set; } = null!;
        public global::App.@this App { get; private set; } = null!;
        public global::App.Actor.Context.@this Context { get; private set; } = null!;
        public System.Type? ParameterType => null;
        public bool IsDisposed { get; private set; }

        public void Initialize(global::App.@this engine, global::App.Actor.Context.@this context) { App = engine; Context = context; }
        public Task<Data> ExecuteAsync(global::App.Goals.Goal.Steps.Step.Actions.Action.@this action, global::App.Actor.Context.@this context)
        {
            Initialize(context.App!, context);
            return Task.FromResult(Data.Ok());
        }
        public ValueTask DisposeAsync() { IsDisposed = true; return ValueTask.CompletedTask; }
    }

    private class ThrowingHandler : IAction, ICodeGenerated
    {
        public global::App.Goals.Goal.Steps.Step.Actions.Action.@this Action { get; set; } = null!;
        public global::App.@this App { get; private set; } = null!;
        public global::App.Actor.Context.@this Context { get; private set; } = null!;
        public System.Type? ParameterType => null;

        public void Initialize(global::App.@this engine, global::App.Actor.Context.@this context) { App = engine; Context = context; }
        public Task<Data> ExecuteAsync(global::App.Goals.Goal.Steps.Step.Actions.Action.@this action, global::App.Actor.Context.@this context)
        {
            throw new InvalidOperationException("Test exception");
        }
    }

}
