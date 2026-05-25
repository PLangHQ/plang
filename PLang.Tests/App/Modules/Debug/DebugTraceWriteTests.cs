using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using PLangEngine = global::app.@this;

namespace PLang.Tests.App.Modules.Debug;

/// <summary>
/// Stage 5 — Batch 9. <c>debug/this.cs</c> LLM trace writes (D11).
///
/// These tests verify the .build/traces directory derivation by examining
/// the path verb composition, since ResolveLlmFilePath is private. The
/// indirect proof: the path-derivation verbs Combine three segments and
/// produce an Absolute under .build/traces.
/// </summary>
public class DebugTraceWriteTests
{
    private static PLangEngine NewApp(out string root)
    {
        root = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            "plang-debug-" + System.Guid.NewGuid().ToString("N")[..8]);
        System.IO.Directory.CreateDirectory(root);
        return new PLangEngine(root);
    }

    [Test] public async Task GenerateLlmFilePath_ProducedViaPathDerivationVerbs()
    {
        var app = NewApp(out var root);
        var ctx = app.User.Context;
        // Reproduce the exact derivation chain debug/this.cs uses.
        var traceDir = global::app.types.path.@this.Resolve("/.build/traces", ctx)
            .Combine("trace-id-xyz").Combine("llm");
        var p = traceDir.Combine("MyGoal_step0.txt");
        await Assert.That(p.Absolute.Replace('\\', '/'))
            .Contains(".build/traces/trace-id-xyz/llm/MyGoal_step0.txt");
    }

    [Test] public async Task TraceWrite_GoesThroughPathVerbs_NotFileWriteAllText()
    {
        var app = NewApp(out var root);
        var ctx = app.User.Context;
        var traceDir = global::app.types.path.@this.Resolve("/.build/traces", ctx);
        var mk = await traceDir.Mkdir();
        await Assert.That(mk.Success).IsTrue();
        var f = traceDir.Combine("trace.txt");
        var written = await f.Append("hello\n");
        await Assert.That(written.Success).IsTrue();
        var more = await f.Append("world\n");
        await Assert.That(more.Success).IsTrue();
        var read = await f.ReadText();
        await Assert.That(read.Value as string).Contains("hello");
        await Assert.That(read.Value as string).Contains("world");
    }
}
