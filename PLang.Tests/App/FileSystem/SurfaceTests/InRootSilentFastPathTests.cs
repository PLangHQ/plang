using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;

namespace PLang.Tests.App.FileSystem.SurfaceTests;

/// <summary>
/// Stage 5 — Batch 10. In-root silent fast-path regression guard.
///
/// AuthGate's <c>IsInRoot()</c> fast-path auto-grants in-root verbs. After
/// the migration, the worry is an over-strict refactor breaks the fast-path
/// and <c>plang --test</c> starts spamming permission prompts on legitimate
/// reads. These tests assert: in-root verbs do NOT invoke <c>output.ask</c>.
/// </summary>
public class InRootSilentFastPathTests
{
    [Test] public async Task InRootRead_DoesNotInvokeOutputAsk()
    {
        // Audit-trail channel sees zero Ask calls for an in-root path.ReadText.
        await Task.CompletedTask; Assert.Fail("Not implemented");
    }

    [Test] public async Task InRootWrite_DoesNotInvokeOutputAsk()
    {
        await Task.CompletedTask; Assert.Fail("Not implemented");
    }

    [Test] public async Task InRootListThenReadEach_BatchOfTen_ZeroAskInvocations()
    {
        // Loop tests amplify any fast-path regression — N files, N asks would be a bug.
        await Task.CompletedTask; Assert.Fail("Not implemented");
    }

    [Test] public async Task InRootLoadAssembly_DoesNotInvokeOutputAsk()
    {
        // Execute verb fast-path must work the same as read/write in-root.
        await Task.CompletedTask; Assert.Fail("Not implemented");
    }
}
