using app.actor.context;
using app.variable;
using app.module;
using app.type.item.path;
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
        await using var engine = TestApp.Create(fixturesDir);

        var capture = new CapturingWriteHandler();
        engine.Module.Register("output", "write", capture);

        // Load the .pr file — full pipeline: filesystem → deserialize → goal
        var loadResult = await engine.Goal.LoadFromFileAsync(engine, global::app.type.item.path.@this.Resolve("FullPipeline.pr", engine.System.Context!));
        await loadResult.IsSuccess();

        // Execute
        var context = engine.User.Context;
        var result = await engine.RunGoalAsync(new GoalCall { Name = "FullPipeline" }, context);
        await result.IsSuccess();

        // Variables set correctly
        await Assert.That((await context.Variable.GetValue("greeting"))).IsEqualTo("Hello");
        await Assert.That((await context.Variable.GetValue("user"))).IsEqualTo("World");
        await Assert.That((await context.Variable.GetValue("message"))).IsEqualTo("Hello, World!");

        // Output captured (variable interpolation in output.write)
        await Assert.That(capture.Lines).Contains("Hello, World!");

        // Defaults resolved — step 0 has defaults: [{ type: "string" }]
        var greetingData = await context.Variable.Get("greeting");
        await Assert.That(greetingData?.Type?.Name).IsEqualTo("text");
    }

    [Test]
    public async Task ReadFile_ReturnMapsResultToVariable()
    {
        // Engine rooted at the fixtures dir (contains testdata.txt and ReadFile.pr).
        var fixturesDir = FindFixturesDir();
        await using var engine = TestApp.Create(fixturesDir);

        // Capture output
        var capture = new CapturingWriteHandler();
        engine.Module.Register("output", "write", capture);

        // Load and execute
        var loadResult = await engine.Goal.LoadFromFileAsync(engine, global::app.type.item.path.@this.Resolve("ReadFile.pr", engine.System.Context!));
        await loadResult.IsSuccess();

        var context = engine.User.Context;
        var result = await engine.RunGoalAsync(new GoalCall { Name = "ReadFile" }, context);
        await result.IsSuccess();

        // Return mapping: file/read returns Data.Ok(file), return: [{ name: "content" }] maps it to %content%
        var content = await context.Variable.GetValue("content");
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
        await using var engine = TestApp.Create(fixturesDir);

        var loadResult = await engine.Goal.LoadFromFileAsync(engine, global::app.type.item.path.@this.Resolve("FilePathsFromRoot.pr", engine.System.Context!));
        await loadResult.IsSuccess();

        var context = engine.User.Context;
        var result = await engine.RunGoalAsync(new GoalCall { Name = "FilePathsFromRoot" }, context);
        await result.IsSuccess();

        // #1: testdata.txt — relative, same folder
        await Assert.That((await context.Variable.GetValue("relative"))!.ToString()).IsEqualTo("Hello from test file");

        // #2: /testdata.txt — absolute from root
        await Assert.That((await context.Variable.GetValue("absolute"))!.ToString()).IsEqualTo("Hello from test file");

        // #4: sub/subdata.txt — subfolder relative
        await Assert.That((await context.Variable.GetValue("subfolder"))!.ToString()).IsEqualTo("Hello from subfolder");

        // #7: ./testdata.txt — explicit current dir
        await Assert.That((await context.Variable.GetValue("dotslash"))!.ToString()).IsEqualTo("Hello from test file");
    }

    [Test]
    public async Task FilePaths_FromSubfolder_AbsoluteRootWorks()
    {
        var fixturesDir = FindFixturesDir();
        await using var engine = TestApp.Create(fixturesDir);

        var loadResult = await engine.Goal.LoadFromFileAsync(engine, global::app.type.item.path.@this.Resolve(System.IO.Path.Combine("sub", "FilePathsFromSub.pr"), engine.System.Context!));
        await loadResult.IsSuccess();

        var context = engine.User.Context;
        var result = await engine.RunGoalAsync(new GoalCall { Name = "FilePathsFromSub" }, context);

        // Step 0 (/testdata.txt) succeeds — absolute paths work from any goal location
        // Step 1 (subdata.txt) fails — relative paths resolve against engine root, not goal folder
        //   so "subdata.txt" → {root}/subdata.txt (not found), NOT {root}/sub/subdata.txt
        // The goal fails on step 1, but step 0 already set %rootAbsolute%
        await Assert.That((await context.Variable.GetValue("rootAbsolute"))!.ToString()).IsEqualTo("Hello from test file");
    }

    [Test]
    public async Task FilePaths_RelativeResolvesAgainstGoalFolder()
    {
        var fixturesDir = FindFixturesDir();
        await using var engine = TestApp.Create(fixturesDir);

        // A goal in /sub/ reads "subdata.txt" (relative)
        // This resolves to {root}/sub/subdata.txt — relative to goal folder
        var goal = await RealGoalLoad.ViaChannel(engine, Make.Goal("SubRelative", "/sub/SubRelative.goal",
            Make.Step("read subdata.txt, write to %content%",
                Make.Action("file", "read", ("path", "subdata.txt")))));
        goal.Steps.Context = engine.User.Context;
        engine.Goal.Add(goal);

        var context = engine.User.Context;
        var result = await engine.RunGoalAsync(goal, context);

        // File found — relative resolves to {root}/sub/subdata.txt (goal folder)
        await result.IsSuccess();
        await Assert.That((await result.Value())!.ToString()).IsEqualTo("Hello from subfolder");
    }

    [Test]
    public async Task FilePaths_ParentTraversal_FromSubfolderToRoot()
    {
        var fixturesDir = FindFixturesDir();
        await using var engine = TestApp.Create(fixturesDir);

        // #3: Goal in /sub/ reads ../testdata.txt — should resolve to {root}/testdata.txt
        var goal = await RealGoalLoad.ViaChannel(engine, Make.Goal("ParentTraversal", "/sub/ParentTraversal.goal",
            Make.Step("read ../testdata.txt, write to %fromParent%",
                Make.Action("file", "read", ("path", "../testdata.txt")))));
        goal.Steps.Context = engine.User.Context;
        engine.Goal.Add(goal);

        var context = engine.User.Context;
        var result = await engine.RunGoalAsync(goal, context);

        await result.IsSuccess();
        await Assert.That((await result.Value())!.ToString()).IsEqualTo("Hello from test file");
    }

    [Test]
    public async Task FilePaths_ParentTraversal_BackAndDown()
    {
        var fixturesDir = FindFixturesDir();
        await using var engine = TestApp.Create(fixturesDir);

        // #8: Goal in /sub/ reads ../sub/subdata.txt — parent then back down
        var goal = await RealGoalLoad.ViaChannel(engine, Make.Goal("ParentAndDown", "/sub/ParentAndDown.goal",
            Make.Step("read ../sub/subdata.txt, write to %backAndDown%",
                Make.Action("file", "read", ("path", "../sub/subdata.txt")))));
        goal.Steps.Context = engine.User.Context;
        engine.Goal.Add(goal);

        var context = engine.User.Context;
        var result = await engine.RunGoalAsync(goal, context);

        await result.IsSuccess();
        await Assert.That((await result.Value())!.ToString()).IsEqualTo("Hello from subfolder");
    }

    [Test]
    public async Task FilePaths_NonexistentFile_ReturnsError()
    {
        var fixturesDir = FindFixturesDir();
        await using var engine = TestApp.Create(fixturesDir);

        // Hand-build a goal that reads a nonexistent file
        var goal = await RealGoalLoad.ViaChannel(engine, Make.Goal("ReadMissing", "/ReadMissing.goal",
            Make.Step("read nonexistent.txt, write to %content%",
                Make.Action("file", "read", ("path", "nonexistent.txt")))));
        goal.Steps.Context = engine.User.Context;
        engine.Goal.Add(goal);

        var context = engine.User.Context;
        var result = await engine.RunGoalAsync(goal, context);

        // file/read returns Data.FromError for missing files
        await result.IsFailure();
        await Assert.That(result.Error).IsNotNull();
    }

    [Test]
    public async Task FilePaths_EscapeAttempt_Blocked()
    {
        var fixturesDir = FindFixturesDir();
        await using var engine = TestApp.Create(fixturesDir);

        // Try to read ../../ — should be blocked by PLangFileSystem
        var goal = await RealGoalLoad.ViaChannel(engine, Make.Goal("ReadEscape", "/ReadEscape.goal",
            Make.Step("read ../../etc/passwd, write to %content%",
                Make.Action("file", "read", ("path", "../../etc/passwd")))));
        goal.Steps.Context = engine.User.Context;
        engine.Goal.Add(goal);

        var context = engine.User.Context;
        var result = await engine.RunGoalAsync(goal, context);

        // PLangFileSystem should block path escape — either throws FileAccessException or returns error
        await result.IsFailure();
    }

    #endregion

    private static string FindFixturesDir()
    {
        var dir = AppContext.BaseDirectory;
        while (dir != null)
        {
            var candidate = System.IO.Path.Combine(dir, "PLang.Tests", "Shared", "Fixtures", "pr");
            if (Directory.Exists(candidate))
                return candidate;
            dir = Directory.GetParent(dir)?.FullName;
        }

        var fallback = System.IO.Path.GetFullPath(System.IO.Path.Combine(
            Directory.GetCurrentDirectory(), "..", "..", "..", "..", "PLang.Tests", "Shared", "Fixtures", "pr"));
        if (Directory.Exists(fallback))
            return fallback;

        throw new DirectoryNotFoundException("Could not find PLang.Tests/Shared/Fixtures/pr/");
    }

    /// <summary>
    /// Captures output.write calls for assertion. Same pattern as StartGoalTests.
    /// </summary>
    private class CapturingWriteHandler : IAction, ICodeGenerated
    {
        public List<string> Lines { get; } = new();

        public global::app.goal.step.action.@this Action { get; set; } = null!;
        public global::app.@this App { get; private set; } = null!;
        public global::app.actor.context.@this Context { get; private set; } = null!;
        public System.Type? ParameterType => null;

        public Task<global::app.error.IError?> Attach(global::app.goal.step.action.@this action, global::app.actor.context.@this context)
        { Action = action; App = context.App!; Context = context; return Task.FromResult<global::app.error.IError?>(null); }

        public async Task<Data> Execute()
        {
            var action = Action;
            var context = Context;
            var contentData = action?.Parameters.FirstOrDefault(d => string.Equals(d.Name, "Data", StringComparison.OrdinalIgnoreCase));
            object? raw = contentData.Peek();
            object? content = (raw as global::app.type.item.text.@this)?.Clr<string>() ?? raw;
            if (content is string str && str.Contains('%'))
            {
                var resolved = await context.Variable.Resolve(str);
                if (resolved != str)
                    content = resolved;
            }
            if (content != null)
                Lines.Add(content.ToString()!);
            return context.App.Ok();
        }
    }
}
