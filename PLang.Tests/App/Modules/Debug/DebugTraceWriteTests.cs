using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using PLangEngine = global::app.@this;

namespace PLang.Tests.App.Modules.Debug;

/// <summary>
/// <c>debug/this.cs</c> LLM trace writes.
///
/// Drives <c>EmitLlmBlock</c> + <c>ResolveLlmFilePath</c> directly —
/// the actual handler methods. A mutation that reverted
/// <c>_currentLlmFilePath.Append(...)</c> to <c>System.IO.File.AppendAllText</c>
/// would not flip these tests (both end up writing to disk), so we also
/// assert the underlying path is a <see cref="global::app.type.path.@this"/>
/// instance — the typed channel is the audit-gate.
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
        var context = app.User.Context;
        var resolved = app.Debug.ResolveLlmFilePath(context);
        // Typed channel: ResolveLlmFilePath must return a Path object (the
        // .Absolute reach is auth-gated). A future mutation reverting to
        // System.IO.Path.Combine + string would break this signature.
        await Assert.That(resolved).IsNotNull();
        await Assert.That(resolved is global::app.type.path.@this).IsTrue();
        // And the derivation lands under .build/traces.
        await Assert.That(resolved.Absolute.Replace('\\', '/'))
            .Contains(".build/traces/");
    }

    [Test] public async Task TraceWrite_GoesThroughPathVerbs_NotFileWriteAllText()
    {
        var app = NewApp(out var root);
        var context = app.User.Context;
        // Pre-stage the trace file path the way the LLM event subscriber does.
        app.Debug._currentLlmFilePath = app.Debug.ResolveLlmFilePath(context);
        // Drive a trace emit. Append routes through AuthGate(Write); in-root
        // fast-passes. If anyone reverts to System.IO.File.AppendAllText the
        // PLNG002 analyzer fails the build — but we additionally verify the
        // bytes land on disk.
        app.Debug.EmitLlmBlock("LLM TEST", new[] { "line one", "line two" }, context, toFile: true);
        // Read back via the same gated verb.
        var read = await app.Debug._currentLlmFilePath!.ReadText();
        await read.IsSuccess();
        var content = (await read.Value()) as string ?? "";
        await Assert.That(content).Contains("LLM TEST");
        await Assert.That(content).Contains("line one");
        await Assert.That(content).Contains("line two");
    }
}
