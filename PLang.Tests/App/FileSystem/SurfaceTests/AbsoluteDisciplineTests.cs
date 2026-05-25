using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;

namespace PLang.Tests.App.FileSystem.SurfaceTests;

/// <summary>
/// Stage 5 — Batch 8. <c>.Absolute</c> discipline (D13).
///
/// <c>path.Absolute</c> is an easy-to-misuse escape hatch — construction is
/// free, so reaching for .Absolute outside <c>app.types.path.**</c> means a
/// third-party API is about to touch the filesystem with no gate. The
/// handler MUST <c>await path.Authorize(verb)</c> first and check
/// <c>auth.Success</c>.
///
/// These tests pin the rule with a mutation-test pattern: a fixture that
/// instruments an "Authorize-then-Absolute" call site, with the assertion
/// being "if the Authorize call is removed (mutation), the test fails".
/// </summary>
public class AbsoluteDisciplineTests
{
    [Test] public async Task TakeOverApi_AuthorizeFirst_OutOfRootDenial_PreventsAbsoluteUse()
    {
        // dbPath.Authorize(Write) returns Fail on denial — handler must early-return
        // before sqliteOpen(dbPath.Absolute) ever runs.
        await Task.CompletedTask; Assert.Fail("Not implemented");
    }

    [Test] public async Task TakeOverApi_AuthorizeFirst_InRootGrant_AllowsAbsoluteUse()
    {
        // In-root: Authorize returns Ok silently; .Absolute is then safe to read.
        await Task.CompletedTask; Assert.Fail("Not implemented");
    }

    [Test] public async Task MutationGuard_RemovingAuthorizeBeforeAbsolute_BreaksThisTest()
    {
        // Documented mutation test: per CLAUDE.md, tester announces this mutation,
        // edits a handler to drop the Authorize check, confirms this test fails.
        await Task.CompletedTask; Assert.Fail("Not implemented");
    }

    [Test] public async Task PathInternals_ReachForAbsolute_IsAllowed_NoDiagnostic()
    {
        // Inside app.types.path.** the .Absolute reach is fine — verbs fire AuthGate.
        await Task.CompletedTask; Assert.Fail("Not implemented");
    }

    [Test] public async Task DiagnosticString_UsesAbsolute_InErrorMessage_IsAllowed()
    {
        // Allowed exception: ToString / error-message .Absolute reaches don't read
        // the file — they're stringification of the path itself.
        await Task.CompletedTask; Assert.Fail("Not implemented");
    }
}
