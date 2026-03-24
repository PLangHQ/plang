using PLang.Runtime2.Engine.Context;
using PLang.Runtime2.Engine.Memory;
using PLang.Runtime2.modules;
using PLang.SafeFileSystem;
using Path = System.IO.Path;
using File = System.IO.File;
using Directory = System.IO.Directory;

namespace PLang.Tests.Runtime2.Core;

/// <summary>
/// Tests the full .pr pipeline: file on disk → engine load → deserialization → execution.
/// Uses hand-crafted .pr fixtures in PLang.Tests/Runtime2/Fixtures/pr/.
/// </summary>
public class PrPipelineTests
{
    [Test]
    public async Task FullPipeline_LoadAndExecute_VariablesOutputDefaults()
    {
        await using var engine = new PLang.Runtime2.Engine.@this("/app");

        // Capture output.write calls
        var capture = new CapturingWriteHandler();
        engine.Modules.Register("output", "write", capture);

        // Point engine filesystem at the fixtures directory
        var fixturesDir = FindFixturesDir();
        engine.FileSystem = new PLangFileSystem(fixturesDir, "");

        // Load the .pr file — full pipeline: filesystem → deserialize → goal
        var loadResult = await engine.LoadGoalFromFileAsync("FullPipeline.pr");
        await Assert.That(loadResult.Success).IsTrue();

        // Execute
        using var context = engine.CreateContext();
        var result = await engine.RunGoalAsync(new GoalCall { Name = "FullPipeline" }, context);
        await Assert.That(result.Success).IsTrue();

        // Variables set correctly
        await Assert.That(context.MemoryStack.GetValue("greeting")).IsEqualTo("Hello");
        await Assert.That(context.MemoryStack.GetValue("user")).IsEqualTo("World");
        await Assert.That(context.MemoryStack.GetValue("message")).IsEqualTo("Hello, World!");

        // Output captured (variable interpolation in output.write)
        await Assert.That(capture.Lines).Contains("Hello, World!");

        // Defaults resolved — step 0 has defaults: [{ type: "string" }]
        var greetingData = context.MemoryStack.Get("greeting");
        await Assert.That(greetingData?.Type?.Value).IsEqualTo("string");
    }

    [Test]
    public async Task ReadFile_ReturnMapsResultToVariable()
    {
        await using var engine = new PLang.Runtime2.Engine.@this("/app");

        // Capture output
        var capture = new CapturingWriteHandler();
        engine.Modules.Register("output", "write", capture);

        // Point engine filesystem at fixtures dir (contains testdata.txt and ReadFile.pr)
        var fixturesDir = FindFixturesDir();
        engine.FileSystem = new PLangFileSystem(fixturesDir, "");

        // Load and execute
        var loadResult = await engine.LoadGoalFromFileAsync("ReadFile.pr");
        await Assert.That(loadResult.Success).IsTrue();

        using var context = engine.CreateContext();
        var result = await engine.RunGoalAsync(new GoalCall { Name = "ReadFile" }, context);
        await Assert.That(result.Success).IsTrue();

        // Return mapping: file/read returns Data.Ok(file), return: [{ name: "content" }] maps it to %content%
        var content = context.MemoryStack.GetValue("content");
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
        await using var engine = new PLang.Runtime2.Engine.@this("/app");
        var fixturesDir = FindFixturesDir();
        engine.FileSystem = new PLangFileSystem(fixturesDir, "");

        var loadResult = await engine.LoadGoalFromFileAsync("FilePathsFromRoot.pr");
        await Assert.That(loadResult.Success).IsTrue();

        using var context = engine.CreateContext();
        var result = await engine.RunGoalAsync(new GoalCall { Name = "FilePathsFromRoot" }, context);
        await Assert.That(result.Success).IsTrue();

        // #1: testdata.txt — relative, same folder
        await Assert.That(context.MemoryStack.GetValue("relative")!.ToString()).IsEqualTo("Hello from test file");

        // #2: /testdata.txt — absolute from root
        await Assert.That(context.MemoryStack.GetValue("absolute")!.ToString()).IsEqualTo("Hello from test file");

        // #4: sub/subdata.txt — subfolder relative
        await Assert.That(context.MemoryStack.GetValue("subfolder")!.ToString()).IsEqualTo("Hello from subfolder");

        // #7: ./testdata.txt — explicit current dir
        await Assert.That(context.MemoryStack.GetValue("dotslash")!.ToString()).IsEqualTo("Hello from test file");
    }

    [Test]
    public async Task FilePaths_FromSubfolder_AbsoluteRootWorks()
    {
        await using var engine = new PLang.Runtime2.Engine.@this("/app");
        var fixturesDir = FindFixturesDir();
        engine.FileSystem = new PLangFileSystem(fixturesDir, "");

        var loadResult = await engine.LoadGoalFromFileAsync(Path.Combine("sub", "FilePathsFromSub.pr"));
        await Assert.That(loadResult.Success).IsTrue();

        using var context = engine.CreateContext();
        var result = await engine.RunGoalAsync(new GoalCall { Name = "FilePathsFromSub" }, context);

        // Step 0 (/testdata.txt) succeeds — absolute paths work from any goal location
        // Step 1 (subdata.txt) fails — relative paths resolve against engine root, not goal folder
        //   so "subdata.txt" → {root}/subdata.txt (not found), NOT {root}/sub/subdata.txt
        // The goal fails on step 1, but step 0 already set %rootAbsolute%
        await Assert.That(context.MemoryStack.GetValue("rootAbsolute")!.ToString()).IsEqualTo("Hello from test file");
    }

    [Test]
    public async Task FilePaths_RelativeResolvesAgainstGoalFolder()
    {
        await using var engine = new PLang.Runtime2.Engine.@this("/app");
        var fixturesDir = FindFixturesDir();
        engine.FileSystem = new PLangFileSystem(fixturesDir, "");

        // A goal in /sub/ reads "subdata.txt" (relative)
        // This resolves to {root}/sub/subdata.txt — relative to goal folder
        var goal = new PLang.Runtime2.Engine.Goals.Goal.@this
        {
            Name = "SubRelative",
            Path = "/sub/SubRelative.goal",
            Steps = new PLang.Runtime2.Engine.Goals.Goal.Steps.@this
            {
                new PLang.Runtime2.Engine.Goals.Goal.Steps.Step.@this
                {
                    Index = 0,
                    Text = "read subdata.txt, write to %content%",
                    Actions = new PLang.Runtime2.Engine.Goals.Goal.Steps.Step.Actions.@this
                    {
                        new PLang.Runtime2.Engine.Goals.Goal.Steps.Step.Actions.Action.@this
                        {
                            Module = "file",
                            ActionName = "read",
                            Parameters = new List<Data> { new Data("path", "subdata.txt") },
                            Return = new List<Data> { new Data("content") }
                        }
                    }
                }
            }
        };
        engine.Goals.Add(goal);

        using var context = engine.CreateContext();
        var result = await engine.RunGoalAsync(goal, context);

        // File found — relative resolves to {root}/sub/subdata.txt (goal folder)
        await Assert.That(result.Success).IsTrue();
        await Assert.That(context.MemoryStack.GetValue("content")!.ToString()).IsEqualTo("Hello from subfolder");
    }

    [Test]
    public async Task FilePaths_ParentTraversal_FromSubfolderToRoot()
    {
        await using var engine = new PLang.Runtime2.Engine.@this("/app");
        var fixturesDir = FindFixturesDir();
        engine.FileSystem = new PLangFileSystem(fixturesDir, "");

        // #3: Goal in /sub/ reads ../testdata.txt — should resolve to {root}/testdata.txt
        var goal = new PLang.Runtime2.Engine.Goals.Goal.@this
        {
            Name = "ParentTraversal",
            Path = "/sub/ParentTraversal.goal",
            Steps = new PLang.Runtime2.Engine.Goals.Goal.Steps.@this
            {
                new PLang.Runtime2.Engine.Goals.Goal.Steps.Step.@this
                {
                    Index = 0,
                    Text = "read ../testdata.txt, write to %fromParent%",
                    Actions = new PLang.Runtime2.Engine.Goals.Goal.Steps.Step.Actions.@this
                    {
                        new PLang.Runtime2.Engine.Goals.Goal.Steps.Step.Actions.Action.@this
                        {
                            Module = "file",
                            ActionName = "read",
                            Parameters = new List<Data> { new Data("path", "../testdata.txt") },
                            Return = new List<Data> { new Data("fromParent") }
                        }
                    }
                }
            }
        };
        engine.Goals.Add(goal);

        using var context = engine.CreateContext();
        var result = await engine.RunGoalAsync(goal, context);

        await Assert.That(result.Success).IsTrue();
        await Assert.That(context.MemoryStack.GetValue("fromParent")!.ToString()).IsEqualTo("Hello from test file");
    }

    [Test]
    public async Task FilePaths_ParentTraversal_BackAndDown()
    {
        await using var engine = new PLang.Runtime2.Engine.@this("/app");
        var fixturesDir = FindFixturesDir();
        engine.FileSystem = new PLangFileSystem(fixturesDir, "");

        // #8: Goal in /sub/ reads ../sub/subdata.txt — parent then back down
        var goal = new PLang.Runtime2.Engine.Goals.Goal.@this
        {
            Name = "ParentAndDown",
            Path = "/sub/ParentAndDown.goal",
            Steps = new PLang.Runtime2.Engine.Goals.Goal.Steps.@this
            {
                new PLang.Runtime2.Engine.Goals.Goal.Steps.Step.@this
                {
                    Index = 0,
                    Text = "read ../sub/subdata.txt, write to %backAndDown%",
                    Actions = new PLang.Runtime2.Engine.Goals.Goal.Steps.Step.Actions.@this
                    {
                        new PLang.Runtime2.Engine.Goals.Goal.Steps.Step.Actions.Action.@this
                        {
                            Module = "file",
                            ActionName = "read",
                            Parameters = new List<Data> { new Data("path", "../sub/subdata.txt") },
                            Return = new List<Data> { new Data("backAndDown") }
                        }
                    }
                }
            }
        };
        engine.Goals.Add(goal);

        using var context = engine.CreateContext();
        var result = await engine.RunGoalAsync(goal, context);

        await Assert.That(result.Success).IsTrue();
        await Assert.That(context.MemoryStack.GetValue("backAndDown")!.ToString()).IsEqualTo("Hello from subfolder");
    }

    [Test]
    public async Task FilePaths_NonexistentFile_ReturnsError()
    {
        await using var engine = new PLang.Runtime2.Engine.@this("/app");
        var fixturesDir = FindFixturesDir();
        engine.FileSystem = new PLangFileSystem(fixturesDir, "");

        // Hand-build a goal that reads a nonexistent file
        var goal = new PLang.Runtime2.Engine.Goals.Goal.@this
        {
            Name = "ReadMissing",
            Path = "/ReadMissing.goal",
            Steps = new PLang.Runtime2.Engine.Goals.Goal.Steps.@this
            {
                new PLang.Runtime2.Engine.Goals.Goal.Steps.Step.@this
                {
                    Index = 0,
                    Text = "read nonexistent.txt, write to %content%",
                    Actions = new PLang.Runtime2.Engine.Goals.Goal.Steps.Step.Actions.@this
                    {
                        new PLang.Runtime2.Engine.Goals.Goal.Steps.Step.Actions.Action.@this
                        {
                            Module = "file",
                            ActionName = "read",
                            Parameters = new List<Data> { new Data("path", "nonexistent.txt") },
                            Return = new List<Data> { new Data("content") }
                        }
                    }
                }
            }
        };
        engine.Goals.Add(goal);

        using var context = engine.CreateContext();
        var result = await engine.RunGoalAsync(goal, context);

        // file/read returns Data.FromError for missing files
        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error).IsNotNull();
    }

    [Test]
    public async Task FilePaths_EscapeAttempt_Blocked()
    {
        await using var engine = new PLang.Runtime2.Engine.@this("/app");
        var fixturesDir = FindFixturesDir();
        engine.FileSystem = new PLangFileSystem(fixturesDir, "");

        // Try to read ../../ — should be blocked by PLangFileSystem
        var goal = new PLang.Runtime2.Engine.Goals.Goal.@this
        {
            Name = "ReadEscape",
            Path = "/ReadEscape.goal",
            Steps = new PLang.Runtime2.Engine.Goals.Goal.Steps.@this
            {
                new PLang.Runtime2.Engine.Goals.Goal.Steps.Step.@this
                {
                    Index = 0,
                    Text = "read ../../etc/passwd, write to %content%",
                    Actions = new PLang.Runtime2.Engine.Goals.Goal.Steps.Step.Actions.@this
                    {
                        new PLang.Runtime2.Engine.Goals.Goal.Steps.Step.Actions.Action.@this
                        {
                            Module = "file",
                            ActionName = "read",
                            Parameters = new List<Data> { new Data("path", "../../etc/passwd") },
                            Return = new List<Data> { new Data("content") }
                        }
                    }
                }
            }
        };
        engine.Goals.Add(goal);

        using var context = engine.CreateContext();
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
            var candidate = Path.Combine(dir, "PLang.Tests", "Runtime2", "Fixtures", "pr");
            if (Directory.Exists(candidate))
                return candidate;
            dir = Directory.GetParent(dir)?.FullName;
        }

        var fallback = Path.GetFullPath(Path.Combine(
            Directory.GetCurrentDirectory(), "..", "..", "..", "..", "PLang.Tests", "Runtime2", "Fixtures", "pr"));
        if (Directory.Exists(fallback))
            return fallback;

        throw new DirectoryNotFoundException("Could not find PLang.Tests/Runtime2/Fixtures/pr/");
    }

    /// <summary>
    /// Captures output.write calls for assertion. Same pattern as StartGoalTests.
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

        public Task<Data> CodeGeneratedExecuteAsync(List<Data> parameters, PLang.Runtime2.Engine.@this engine, PLangContext context, List<Data>? defaults = null)
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
}
