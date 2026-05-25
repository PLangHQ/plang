using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;

namespace PLang.Tests.App.FileSystem.PermissionTests.ExecuteVerbTests;

/// <summary>
/// Stage 5 — Batch 6. <c>Execute</c> verb (D8/C5) + <c>path.LoadAssemblyAsync</c>.
///
/// New verb alongside Read/Write/Delete under
/// <c>app.types.path.permission.verb</c>. Authorize prompt becomes
/// "Allow X to execute Y" — semantically distinct from "read Y".
/// <c>path.LoadAssemblyAsync()</c> gates with <c>Verb { Execute = … }</c> and
/// calls <c>Assembly.LoadFrom(this.Absolute)</c> internally — the handler
/// never reaches for .Absolute.
/// </summary>
public class ExecuteVerbTests
{
    [Test] public async Task ExecuteVerb_ExistsInVerbTaxonomy()
    {
        // Verb has an `Execute` slot alongside Read/Write/Delete.
        await Task.CompletedTask; Assert.Fail("Not implemented");
    }

    [Test] public async Task ExecuteVerb_JsonRoundTrip_PreservesShape()
    {
        // Permission storage round-trips the Execute verb correctly.
        await Task.CompletedTask; Assert.Fail("Not implemented");
    }

    [Test] public async Task ExecuteVerb_PromptCopy_DistinguishesFromRead()
    {
        // The Ask text for Execute reads as "execute", not "read".
        await Task.CompletedTask; Assert.Fail("Not implemented");
    }

    [Test] public async Task ReadGrant_DoesNotCoverExecute()
    {
        // A signed Read grant must NOT auto-grant Execute on the same path.
        await Task.CompletedTask; Assert.Fail("Not implemented");
    }

    [Test] public async Task LoadAssemblyAsync_InRoot_ReturnsLoadedAssembly_NoPrompt()
    {
        // path.LoadAssemblyAsync() on an in-root DLL passes the gate silently.
        await Task.CompletedTask; Assert.Fail("Not implemented");
    }

    [Test] public async Task LoadAssemblyAsync_OutOfRoot_StatelessChannel_ReturnsAsk()
    {
        // Out-of-root DLL load surfaces an Execute Ask for the actor to answer.
        await Task.CompletedTask; Assert.Fail("Not implemented");
    }

    [Test] public async Task LoadAssemblyAsync_OutOfRoot_DeniedAnswer_DoesNotLoadAssembly()
    {
        // "n" answer → no Assembly.LoadFrom call; returns Data.Fail.
        await Task.CompletedTask; Assert.Fail("Not implemented");
    }
}
