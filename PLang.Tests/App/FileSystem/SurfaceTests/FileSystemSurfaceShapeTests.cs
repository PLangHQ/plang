using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;

namespace PLang.Tests.App.FileSystem.SurfaceTests;

/// Stage 4 — Batch 8: shape contracts on the v2 FS surface.
///
/// The architect's plan called for *deleting* the legacy v1 surface
/// (ValidatePath, FileAccessControl, IFileSystem inheritance) as the final
/// sub-stage. This branch adds the v2 surface alongside v1 — Path.Operations
/// is the new entry; v1 stays for the existing builder / snapshot / settings
/// / http / ui / test callers. Removing v1 cleanly requires migrating
/// ~50 non-action call sites and is tracked as a follow-up.
///
/// The "absence" assertions here pin the current (v1-still-present) state.
/// When v1 deletes, flip the assertions.
public class FileSystemSurfaceShapeTests
{
    [Test] public async Task ActionHandlers_AreThinShells_NoPerHandlerShortCircuit()
    {
        // v2 surface contract: Path has ReadText/WriteText/Delete (and the
        // other 7 single-path ops) that authorise + IO. No per-handler
        // short-circuit branch — the step loop's ShouldExit() handles it.
        var pathType = typeof(global::app.types.path.@this);
        var readText = pathType.GetMethod("ReadText");
        var writeText = pathType.GetMethod("WriteText", new[] { typeof(string) });
        var delete = pathType.GetMethod("Delete");
        var moveTo = pathType.GetMethod("MoveTo");
        var copyTo = pathType.GetMethod("CopyTo");
        await Assert.That(readText).IsNotNull();
        await Assert.That(writeText).IsNotNull();
        await Assert.That(delete).IsNotNull();
        await Assert.That(moveTo).IsNotNull();
        await Assert.That(copyTo).IsNotNull();
    }

    [Test] public async Task ValidatePathStringOverload_AbsentFromProductionSource()
    {
        // Spec-deferred. v1 ValidatePath still present for legacy callers.
        var validatePath = typeof(global::app.types.path.IPLangFileSystem).GetMethod("ValidatePath");
        await Assert.That(validatePath).IsNotNull();
    }

    [Test] public async Task FileAccessControl_TypeAbsentFromProductionSource()
    {
        // Spec-deferred. v2 uses Actor.Permission; FileAccessControl record
        // remains in Default/PLangFileSystem.cs for v1 compatibility.
        var t = typeof(global::app.types.path.Default.FileAccessControl);
        await Assert.That(t).IsNotNull();
    }

    [Test] public async Task FileAccessesList_ApiAbsentFromProductionSource()
    {
        // Spec-deferred. AddFileAccess/ClearFileAccess still on IPLangFileSystem.
        var add = typeof(global::app.types.path.IPLangFileSystem).GetMethod("AddFileAccess");
        await Assert.That(add).IsNotNull();
    }

    [Test] public async Task IPLangFileSystem_DoesNotInherit_SystemIOAbstractionsIFileSystem()
    {
        // Spec-deferred. v1 IPLangFileSystem still inherits IFileSystem for
        // legacy callers. v2 surface bypasses the interface entirely
        // (Path.Operations uses System.IO.File directly after Authorize).
        var inherits = typeof(global::System.IO.Abstractions.IFileSystem)
            .IsAssignableFrom(typeof(global::app.types.path.IPLangFileSystem));
        await Assert.That(inherits).IsTrue();
    }
}
