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
        engine.Libraries.Register("output", "write", capture);

        // Point engine filesystem at the fixtures directory
        var fixturesDir = FindFixturesDir();
        engine.FileSystem = new PLangFileSystem(fixturesDir, "");

        // Load the .pr file — full pipeline: filesystem → deserialize → goal
        var loadResult = await engine.LoadGoalFromFileAsync("FullPipeline.pr");
        await Assert.That(loadResult.Success).IsTrue();

        // Execute
        using var context = engine.CreateContext();
        var result = await engine.RunGoalAsync("FullPipeline", context);
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
        engine.Libraries.Register("output", "write", capture);

        // Point engine filesystem at fixtures dir (contains testdata.txt and ReadFile.pr)
        var fixturesDir = FindFixturesDir();
        engine.FileSystem = new PLangFileSystem(fixturesDir, "");

        // Load and execute
        var loadResult = await engine.LoadGoalFromFileAsync("ReadFile.pr");
        await Assert.That(loadResult.Success).IsTrue();

        using var context = engine.CreateContext();
        var result = await engine.RunGoalAsync("ReadFile", context);
        await Assert.That(result.Success).IsTrue();

        // Return mapping: file/read returns Data.Ok(file), return: [{ name: "content" }] maps it to %content%
        var content = context.MemoryStack.GetValue("content");
        await Assert.That(content).IsNotNull();

        // The mapped value is a file object — its ToString() returns the file content
        await Assert.That(content!.ToString()).IsEqualTo("Hello from test file");

        // Output.write resolved %content% and wrote it
        await Assert.That(capture.Lines.Count).IsGreaterThanOrEqualTo(1);
    }

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
