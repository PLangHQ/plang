using app.actor.context;
using app.variable;
using app.modules;
using app.types.path;
using File = System.IO.File;
using Directory = System.IO.Directory;

namespace PLang.Tests.App.Core;

/// <summary>
/// Tests the full .pr pipeline: file on disk → engine load → deserialization → execution.
/// Uses hand-crafted .pr fixtures in PLang.Tests/App/Fixtures/pr/.
/// </summary>
public class PrPipelineTests
{
    [Test]
    public async Task FullPipeline_LoadAndExecute_VariablesOutputDefaults()
    {
        var fixturesDir = FindFixturesDir();
        await using var engine = new global::app.@this(fixturesDir);

        var capture = new CapturingWriteHandler();
        engine.Modules.Register("output", "write", capture);

        // Load the .pr file — full pipeline: filesystem → deserialize → goal
        var loadResult = await engine.Goals.LoadFromFileAsync(engine,"FullPipeline.pr");
        await Assert.That(loadResult.Success).IsTrue();

        // Execute
        var context = engine.User.Context;
        var result = await engine.RunGoalAsync(new GoalCall { Name = "FullPipeline" }, context);
        await Assert.That(result.Success).IsTrue();

        // Variables set correctly
        await Assert.That(context.Variables.GetValue("greeting")).IsEqualTo("Hello");
        await Assert.That(context.Variables.GetValue("user")).IsEqualTo("World");
        await Assert.That(context.Variables.GetValue("message")).IsEqualTo("Hello, World!");

        // Output captured (variable interpolation in output.write)
        await Assert.That(capture.Lines).Contains("Hello, World!");

        // Defaults resolved — step 0 has defaults: [{ type: "string" }]
        var greetingData = context.Variables.Get("greeting");
        await Assert.That(greetingData?.Type?.Value).IsEqualTo("string");
    }

    [Test]
    public async Task ReadFile_ReturnMapsResultToVariable()
    {
        // Engine rooted at the fixtures dir (contains testdata.txt and ReadFile.pr).
        var fixturesDir = FindFixturesDir();
        await using var engine = new global::app.@this(fixturesDir);

        // Capture output
        var capture = new CapturingWriteHandler();
        engine.Modules.Register("output", "write", capture);

        // Load and execute
        var loadResult = await engine.Goals.LoadFromFileAsync(engine,"ReadFile.pr");
        await Assert.That(loadResult.Success).IsTrue();

        var context = engine.User.Context;
        var result = await engine.RunGoalAsync(new GoalCall { Name = "ReadFile" }, context);
        await Assert.That(result.Success).IsTrue();

        // Return mapping: file/read returns Data.Ok(file), return: [{ name: "content" }] maps it to %content%
        var content = context.Variables.GetValue("content");
        await Assert.That(content).IsNotNull();

        // The mapped value is a file object — its ToString() returns the file content
        await Assert.That(content!.ToString()).IsEqualTo("Hello from test file");

        // Output.write resolved %content% and wrote it
        await Assert.That(capture.Lines.Count).IsGreaterThanOrEqualTo(1);
    }

    #region File Path Resolution

    [Test]
    public async Task FilePaths_FromRoot_RelativeAbsoluteSubfolderDotSlash()
    {
        var fixturesDir = FindFixturesDir();
        await using var engine = new global::app.@this(fixturesDir);

        var loadResult = await engine.Goals.LoadFromFileAsync(engine,"FilePathsFromRoot.pr");
        await Assert.That(loadResult.Success).IsTrue();

        var context = engine.User.Context;
        var result = await engine.RunGoalAsync(new GoalCall { Name = "FilePathsFromRoot" }, context);
        await Assert.That(result.Success).IsTrue();

        // #1: testdata.txt — relative, same folder
        await Assert.That(context.Variables.GetValue("relative")!.ToString()).IsEqualTo("Hello from test file");

        // #2: /testdata.txt — absolute from root
        await Assert.That(context.Variables.GetValue("absolute")!.ToString()).IsEqualTo("Hello from test file");

        // #4: sub/subdata.txt — subfolder relative
        await Assert.That(context.Variables.GetValue("subfolder")!.ToString()).IsEqualTo("Hello from subfolder");

        // #7: ./testdata.txt — explicit current dir
        await Assert.That(context.Variables.GetValue("dotslash")!.ToString()).IsEqualTo("Hello from test file");
    }

    [Test]
    public async Task FilePaths_FromSubfolder_AbsoluteRootWorks()
    {
        var fixturesDir = FindFixturesDir();
        await using var engine = new global::app.@this(fixturesDir);

        var loadResult = await engine.Goals.LoadFromFileAsync(engine,System.IO.Path.Combine("sub", "FilePathsFromSub.pr"));
        await Assert.That(loadResult.Success).IsTrue();

        var context = engine.User.Context;
        var result = await engine.RunGoalAsync(new GoalCall { Name = "FilePathsFromSub" }, context);

        // Step 0 (/testdata.txt) succeeds — absolute paths work from any goal location
        // Step 1 (subdata.txt) fails — relative paths resolve against engine root, not goal folder
        //   so "subdata.txt" → {root}/subdata.txt (not found), NOT {root}/sub/subdata.txt
        // The goal fails on step 1, but step 0 already set %rootAbsolute%
        await Assert.That(context.Variables.GetValue("rootAbsolute")!.ToString()).IsEqualTo("Hello from test file");
    }

    [Test]
    public async Task FilePaths_RelativeResolvesAgainstGoalFolder()
    {
        var fixturesDir = FindFixturesDir();
        await using var engine = new global::app.@this(fixturesDir);

        // A goal in /sub/ reads "subdata.txt" (relative)
        // This resolves to {root}/sub/subdata.txt — relative to goal folder
        var goal = new global::app.goal.@this
        {
            Name = "SubRelative",
            Path = "/sub/SubRelative.goal",
            Steps = new global::app.goal.steps.@this
            {
                new global::app.goal.steps.step.@this
                {
                    Index = 0,
                    Text = "read subdata.txt, write to %content%",
                    Actions = new global::app.goal.steps.step.actions.@this
                    {
                        new global::app.goal.steps.step.actions.action.@this
                        {
                            Module = "file",
                            ActionName = "read",
                            Parameters = new List<Data> { new Data("path", "subdata.txt") },
                        }
                    }
                }
            }
        };
        engine.Goals.Add(goal);

        var context = engine.User.Context;
        var result = await engine.RunGoalAsync(goal, context);

        // File found — relative resolves to {root}/sub/subdata.txt (goal folder)
        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.Value!.ToString()).IsEqualTo("Hello from subfolder");
    }

    [Test]
    public async Task FilePaths_ParentTraversal_FromSubfolderToRoot()
    {
        var fixturesDir = FindFixturesDir();
        await using var engine = new global::app.@this(fixturesDir);

        // #3: Goal in /sub/ reads ../testdata.txt — should resolve to {root}/testdata.txt
        var goal = new global::app.goal.@this
        {
            Name = "ParentTraversal",
            Path = "/sub/ParentTraversal.goal",
            Steps = new global::app.goal.steps.@this
            {
                new global::app.goal.steps.step.@this
                {
                    Index = 0,
                    Text = "read ../testdata.txt, write to %fromParent%",
                    Actions = new global::app.goal.steps.step.actions.@this
                    {
                        new global::app.goal.steps.step.actions.action.@this
                        {
                            Module = "file",
                            ActionName = "read",
                            Parameters = new List<Data> { new Data("path", "../testdata.txt") },
                        }
                    }
                }
            }
        };
        engine.Goals.Add(goal);

        var context = engine.User.Context;
        var result = await engine.RunGoalAsync(goal, context);

        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.Value!.ToString()).IsEqualTo("Hello from test file");
    }

    [Test]
    public async Task FilePaths_ParentTraversal_BackAndDown()
    {
        var fixturesDir = FindFixturesDir();
        await using var engine = new global::app.@this(fixturesDir);

        // #8: Goal in /sub/ reads ../sub/subdata.txt — parent then back down
        var goal = new global::app.goal.@this
        {
            Name = "ParentAndDown",
            Path = "/sub/ParentAndDown.goal",
            Steps = new global::app.goal.steps.@this
            {
                new global::app.goal.steps.step.@this
                {
                    Index = 0,
                    Text = "read ../sub/subdata.txt, write to %backAndDown%",
                    Actions = new global::app.goal.steps.step.actions.@this
                    {
                        new global::app.goal.steps.step.actions.action.@this
                        {
                            Module = "file",
                            ActionName = "read",
                            Parameters = new List<Data> { new Data("path", "../sub/subdata.txt") },
                        }
                    }
                }
            }
        };
        engine.Goals.Add(goal);

        var context = engine.User.Context;
        var result = await engine.RunGoalAsync(goal, context);

        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.Value!.ToString()).IsEqualTo("Hello from subfolder");
    }

    [Test]
    public async Task FilePaths_NonexistentFile_ReturnsError()
    {
        var fixturesDir = FindFixturesDir();
        await using var engine = new global::app.@this(fixturesDir);

        // Hand-build a goal that reads a nonexistent file
        var goal = new global::app.goal.@this
        {
            Name = "ReadMissing",
            Path = "/ReadMissing.goal",
            Steps = new global::app.goal.steps.@this
            {
                new global::app.goal.steps.step.@this
                {
                    Index = 0,
                    Text = "read nonexistent.txt, write to %content%",
                    Actions = new global::app.goal.steps.step.actions.@this
                    {
                        new global::app.goal.steps.step.actions.action.@this
                        {
                            Module = "file",
                            ActionName = "read",
                            Parameters = new List<Data> { new Data("path", "nonexistent.txt") },
                        }
                    }
                }
            }
        };
        engine.Goals.Add(goal);

        var context = engine.User.Context;
        var result = await engine.RunGoalAsync(goal, context);

        // file/read returns Data.FromError for missing files
        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error).IsNotNull();
    }

    [Test]
    public async Task FilePaths_EscapeAttempt_Blocked()
    {
        var fixturesDir = FindFixturesDir();
        await using var engine = new global::app.@this(fixturesDir);

        // Try to read ../../ — should be blocked by PLangFileSystem
        var goal = new global::app.goal.@this
        {
            Name = "ReadEscape",
            Path = "/ReadEscape.goal",
            Steps = new global::app.goal.steps.@this
            {
                new global::app.goal.steps.step.@this
                {
                    Index = 0,
                    Text = "read ../../etc/passwd, write to %content%",
                    Actions = new global::app.goal.steps.step.actions.@this
                    {
                        new global::app.goal.steps.step.actions.action.@this
                        {
                            Module = "file",
                            ActionName = "read",
                            Parameters = new List<Data> { new Data("path", "../../etc/passwd") },
                        }
                    }
                }
            }
        };
        engine.Goals.Add(goal);

        var context = engine.User.Context;
        var result = await engine.RunGoalAsync(goal, context);

        // PLangFileSystem should block path escape — either throws FileAccessException or returns error
        await Assert.That(result.Success).IsFalse();
    }

    #endregion

    private static string FindFixturesDir()
    {
        var dir = AppContext.BaseDirectory;
        while (dir != null)
        {
            var candidate = System.IO.Path.Combine(dir, "PLang.Tests", "App", "Fixtures", "pr");
            if (Directory.Exists(candidate))
                return candidate;
            dir = Directory.GetParent(dir)?.FullName;
        }

        var fallback = System.IO.Path.GetFullPath(System.IO.Path.Combine(
            Directory.GetCurrentDirectory(), "..", "..", "..", "..", "PLang.Tests", "App", "Fixtures", "pr"));
        if (Directory.Exists(fallback))
            return fallback;

        throw new DirectoryNotFoundException("Could not find PLang.Tests/App/Fixtures/pr/");
    }

    /// <summary>
    /// Captures output.write calls for assertion. Same pattern as StartGoalTests.
    /// </summary>
    private class CapturingWriteHandler : IAction, ICodeGenerated
    {
        public List<string> Lines { get; } = new();

        public global::app.goal.steps.step.actions.action.@this Action { get; set; } = null!;
        public global::app.@this App { get; private set; } = null!;
        public global::app.actor.context.@this Context { get; private set; } = null!;
        public System.Type? ParameterType => null;

        public Task<Data> ExecuteAsync(global::app.goal.steps.step.actions.action.@this action, global::app.actor.context.@this context)
        {
            App = context.App!;
            Context = context;
            var contentData = action?.Parameters.FirstOrDefault(d => string.Equals(d.Name, "Data", StringComparison.OrdinalIgnoreCase));
            object? content = contentData?.Value;
            if (content is string str && str.Contains('%'))
            {
                var resolved = context.Variables.Resolve(str);
                if (resolved != str)
                    content = resolved;
            }
            if (content != null)
                Lines.Add(content.ToString()!);
            return Task.FromResult(Data.Ok());
        }
    }
}
