using PLang.Runtime2.Engine.Context;
using PLang.Runtime2.Engine.Memory;
using PLang.Runtime2.modules;
using Path = System.IO.Path;

namespace PLang.Tests.Runtime2.Core;

public class StartGoalTests
{
    #region Programmatic Construction

    [Test]
    public async Task StartGoal_Programmatic_SetsVariablesAndWritesOutput()
    {
        await using var engine = new PLang.Runtime2.Engine.@this("/app");

        // Replace output.write with capturing version
        var capture = new CapturingWriteHandler();
        engine.Libraries.Register("output", "write", capture);

        var goal = new Goal
        {
            Name = "Start",
            Path = "/Start.goal",
            Steps = new GoalSteps
            {
                MakeStep("variable", "set",
                    new Dictionary<string, object?> { { "name", "name" }, { "value", "Plang" } },
                    index: 0, text: "set %name% = \"Plang\""),
                MakeStep("output", "write",
                    new Dictionary<string, object?> { { "content", "%name%" } },
                    index: 1, text: "write out %name%"),
                MakeStep("variable", "set",
                    new Dictionary<string, object?> { { "name", "newVarName" }, { "value", "%name%" } },
                    index: 2, text: "set %newVarName% = %name%"),
                MakeStep("output", "write",
                    new Dictionary<string, object?> { { "content", "NewVar: %newVarName%" } },
                    index: 3, text: "write out \"NewVar: %newVarName%\"")
            }
        };
        engine.Goals.Add(goal);

        using var context = engine.CreateContext();
        var result = await engine.RunGoalAsync(goal, context);

        await Assert.That(result.Success).IsTrue();

        // Check variables
        await Assert.That(context.MemoryStack.GetValue("name")).IsEqualTo("Plang");
        await Assert.That(context.MemoryStack.GetValue("newVarName")).IsEqualTo("Plang");

        // Check output
        await Assert.That(capture.Lines).Contains("Plang");
        await Assert.That(capture.Lines).Contains("NewVar: Plang");
    }

    #endregion

    #region Load from .pr.json

    [Test]
    public async Task StartGoal_LoadFromPrJson_SetsVariablesAndWritesOutput()
    {
        await using var engine = new PLang.Runtime2.Engine.@this("/app");

        // Replace output.write with capturing version
        var capture = new CapturingWriteHandler();
        engine.Libraries.Register("output", "write", capture);

        // Find the .pr.json file and set FileSystem root to repo root so it's accessible
        var prJsonPath = FindPrJsonPath();
        var repoRoot = Path.GetDirectoryName(Path.GetDirectoryName(prJsonPath))!; // parent of Tests/Builder
        engine.FileSystem = new PLang.SafeFileSystem.PLangFileSystem(repoRoot, "");

        var loadResult = await engine.LoadGoalFromFileAsync(prJsonPath);
        await Assert.That(loadResult.Success).IsTrue();

        using var context = engine.CreateContext();
        var result = await engine.RunGoalAsync("Start", context);

        await Assert.That(result.Success).IsTrue();

        // Check variables
        await Assert.That(context.MemoryStack.GetValue("name")).IsEqualTo("Plang");
        await Assert.That(context.MemoryStack.GetValue("newVarName")).IsEqualTo("Plang");

        // Check output
        await Assert.That(capture.Lines).Contains("Plang");
        await Assert.That(capture.Lines).Contains("NewVar: Plang");
    }

    #endregion

    #region Variable Resolution Unit Tests

    [Test]
    public async Task ResolveValue_FullVariableReference_ReturnsTypedValue()
    {
        await using var engine = new PLang.Runtime2.Engine.@this("/app");

        var goal = new Goal
        {
            Name = "Test",
            Path = "/Test.goal",
            Steps = new GoalSteps
            {
                MakeStep("variable", "set",
                    new Dictionary<string, object?> { { "name", "myVar" }, { "value", "Hello" } },
                    index: 0, text: "set myVar"),
                MakeStep("variable", "set",
                    new Dictionary<string, object?> { { "name", "result" }, { "value", "%myVar%" } },
                    index: 1, text: "set result = %myVar%")
            }
        };
        engine.Goals.Add(goal);

        using var context = engine.CreateContext();
        var result = await engine.RunGoalAsync(goal, context);

        await Assert.That(result.Success).IsTrue();
        await Assert.That(context.MemoryStack.GetValue("result")).IsEqualTo("Hello");
    }

    [Test]
    public async Task ResolveValue_StringInterpolation_ReturnsInterpolatedString()
    {
        await using var engine = new PLang.Runtime2.Engine.@this("/app");

        var capture = new CapturingWriteHandler();
        engine.Libraries.Register("output", "write", capture);

        var goal = new Goal
        {
            Name = "Test",
            Path = "/Test.goal",
            Steps = new GoalSteps
            {
                MakeStep("variable", "set",
                    new Dictionary<string, object?> { { "name", "user" }, { "value", "World" } },
                    index: 0, text: "set user"),
                MakeStep("output", "write",
                    new Dictionary<string, object?> { { "content", "Hello %user%!" } },
                    index: 1, text: "write Hello %user%!")
            }
        };
        engine.Goals.Add(goal);

        using var context = engine.CreateContext();
        var result = await engine.RunGoalAsync(goal, context);

        await Assert.That(result.Success).IsTrue();
        await Assert.That(capture.Lines).Contains("Hello World!");
    }

    [Test]
    public async Task ResolveValue_LiteralString_RemainsUnchanged()
    {
        await using var engine = new PLang.Runtime2.Engine.@this("/app");

        var capture = new CapturingWriteHandler();
        engine.Libraries.Register("output", "write", capture);

        var goal = new Goal
        {
            Name = "Test",
            Path = "/Test.goal",
            Steps = new GoalSteps
            {
                MakeStep("output", "write",
                    new Dictionary<string, object?> { { "content", "no variables here" } },
                    index: 0, text: "write literal")
            }
        };
        engine.Goals.Add(goal);

        using var context = engine.CreateContext();
        var result = await engine.RunGoalAsync(goal, context);

        await Assert.That(result.Success).IsTrue();
        await Assert.That(capture.Lines).Contains("no variables here");
    }

    [Test]
    public async Task ResolveValue_MissingVariable_ResolvesToEmptyString()
    {
        await using var engine = new PLang.Runtime2.Engine.@this("/app");

        var capture = new CapturingWriteHandler();
        engine.Libraries.Register("output", "write", capture);

        var goal = new Goal
        {
            Name = "Test",
            Path = "/Test.goal",
            Steps = new GoalSteps
            {
                MakeStep("output", "write",
                    new Dictionary<string, object?> { { "content", "Value: %unknown%" } },
                    index: 0, text: "write with unknown var")
            }
        };
        engine.Goals.Add(goal);

        using var context = engine.CreateContext();
        var result = await engine.RunGoalAsync(goal, context);

        await Assert.That(result.Success).IsTrue();
        await Assert.That(capture.Lines).Contains("Value: ");
    }

    [Test]
    public async Task ResolveValue_FullMissingVariable_ResolvesToNull()
    {
        await using var engine = new PLang.Runtime2.Engine.@this("/app");

        var goal = new Goal
        {
            Name = "Test",
            Path = "/Test.goal",
            Steps = new GoalSteps
            {
                MakeStep("variable", "set",
                    new Dictionary<string, object?> { { "name", "result" }, { "value", "%nonexistent%" } },
                    index: 0, text: "set result = %nonexistent%")
            }
        };
        engine.Goals.Add(goal);

        using var context = engine.CreateContext();
        var result = await engine.RunGoalAsync(goal, context);

        await Assert.That(result.Success).IsTrue();
        await Assert.That(context.MemoryStack.GetValue("result")).IsNull();
    }

    #endregion

    #region Helpers

    private static Step MakeStep(string actionClass, string method, IDictionary<string, object?> parameters, int index = 0, string text = "")
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
                    Parameters = parameters.Select(kv => new Data(kv.Key, kv.Value)).ToList(),
                    Return = null
                }
            }
        };
    }

    private static string FindPrJsonPath()
    {
        // Walk up from test output directory to find the repo root
        var dir = AppContext.BaseDirectory;
        while (dir != null)
        {
            var candidate = Path.Combine(dir, "Tests", "Builder", "Start.pr.json");
            if (File.Exists(candidate))
                return candidate;
            dir = Directory.GetParent(dir)?.FullName;
        }

        // Fallback: try relative to working directory
        var fallback = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "..", "Tests", "Builder", "Start.pr.json"));
        if (File.Exists(fallback))
            return fallback;

        throw new FileNotFoundException("Could not find Tests/Builder/Start.pr.json");
    }

    /// <summary>
    /// A test handler that captures written content instead of writing to Console.
    /// Implements IAction + ICodeGenerated manually since the source generator doesn't run on test projects.
    /// </summary>
    private class CapturingWriteHandler : IAction, ICodeGenerated
    {
        public List<string> Lines { get; } = new();

        public PLang.Runtime2.Engine.@this Engine { get; private set; } = null!;
        public PLangContext Context { get; private set; } = null!;
        public System.Type? ParameterType => null;

        public void Initialize(PLang.Runtime2.Engine.@this engine, PLangContext context)
        {
            Engine = engine;
            Context = context;
        }

        public Task<Data> ExecuteAsync(object? parameters) => Task.FromResult(Data.Ok());

        public Task<Data> CodeGeneratedExecuteAsync(List<Data> parameters, PLang.Runtime2.Engine.@this engine, PLangContext context)
        {
            Initialize(engine, context);
            var contentData = parameters.FirstOrDefault(d => string.Equals(d.Name, "content", StringComparison.OrdinalIgnoreCase));
            object? content = contentData?.Value;
            if (content is string str && str.Contains('%'))
            {
                var fullMatch = System.Text.RegularExpressions.Regex.Match(str, @"^%([^%]+)%$");
                if (fullMatch.Success)
                    content = context.MemoryStack.GetValue(fullMatch.Groups[1].Value);
                else
                    content = System.Text.RegularExpressions.Regex.Replace(str, @"%([^%]+)%",
                        m => context.MemoryStack.GetValue(m.Groups[1].Value)?.ToString() ?? "");
            }
            if (content != null)
                Lines.Add(content.ToString()!);
            return Task.FromResult(Data.Ok());
        }
    }

    #endregion
}
