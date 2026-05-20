using TUnit.Core;

namespace PLang.Tests.App.FileSystem.SurfaceTests;

/// Stage 4 — Batch 8: shape contracts on the new IPLangFileSystem v2 — the
/// old surface is gone, action handlers are thin shells, no `IFileSystem`
/// inheritance. These are static-survey tests over production source.
public class FileSystemSurfaceShapeTests
{
    [Test] public Task ActionHandlers_AreThinShells_NoPerHandlerShortCircuit()  { Assert.Fail("Not implemented"); return Task.CompletedTask; }
    [Test] public Task ValidatePathStringOverload_AbsentFromProductionSource()  { Assert.Fail("Not implemented"); return Task.CompletedTask; }
    [Test] public Task FileAccessControl_TypeAbsentFromProductionSource()       { Assert.Fail("Not implemented"); return Task.CompletedTask; }
    [Test] public Task FileAccessesList_ApiAbsentFromProductionSource()         { Assert.Fail("Not implemented"); return Task.CompletedTask; }
    [Test] public Task IPLangFileSystem_DoesNotInherit_SystemIOAbstractionsIFileSystem() { Assert.Fail("Not implemented"); return Task.CompletedTask; }
}
