using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using System.Linq;

namespace PLang.Tests.App.FileSystem.SurfaceTests;

/// <summary>
/// Shape contracts on the path verb surface, post Stage 8.
///
/// The legacy v1 surface (IPLangFileSystem, PLangFileSystem, FileAccessControl,
/// the System.IO.Abstractions wrapper layer) is now <b>deleted</b>. These
/// assertions — formerly "spec-deferred, flip when v1 deletes" — are flipped:
/// they pin the absence. String-based reflection so the file compiles without
/// the removed types.
/// </summary>
public class FileSystemSurfaceShapeTests
{
    private static System.Reflection.Assembly AppAssembly => typeof(global::app.@this).Assembly;

    [Test] public async Task ActionHandlers_AreThinShells_NoPerHandlerShortCircuit()
    {
        // The path verb surface lives on the abstract base — ReadText/WriteText/
        // Delete/MoveTo/CopyTo. Handlers are thin shells over it.
        var pathType = typeof(global::app.type.path.@this);
        await Assert.That(pathType.GetMethod("ReadText")).IsNotNull();
        await Assert.That(pathType.GetMethod("WriteText", new[] { typeof(string) })).IsNotNull();
        await Assert.That(pathType.GetMethod("Delete", System.Type.EmptyTypes)).IsNotNull();
        await Assert.That(pathType.GetMethod("MoveTo")).IsNotNull();
        await Assert.That(pathType.GetMethod("CopyTo")).IsNotNull();
    }

    [Test] public async Task IPLangFileSystem_AbsentFromProductionAssembly()
    {
        await Assert.That(AppAssembly.GetType("app.type.path.IPLangFileSystem")).IsNull();
    }

    [Test] public async Task FileAccessControl_TypeAbsentFromProductionAssembly()
    {
        // v2 uses Actor.Permission — the FileAccessControl root-jail record is gone.
        await Assert.That(AppAssembly.GetType("app.type.path.Default.FileAccessControl")).IsNull();
    }

    [Test] public async Task PLangFileSystem_WrapperLayer_AbsentFromProductionAssembly()
    {
        // The System.IO.Abstractions wrapper layer is deleted.
        foreach (var name in new[]
        {
            "app.type.path.Default.PLangFileSystem",
            "app.type.path.Default.PLangFile",
            "app.type.path.Default.PLangDirectoryWrapper",
            "app.type.path.Default.PLangPath",
        })
        {
            await Assert.That(AppAssembly.GetType(name)).IsNull();
        }
    }

    [Test] public async Task ProductionAssembly_DoesNotReference_SystemIOAbstractions()
    {
        // The System.IO.Abstractions package is dropped — no referenced assembly
        // named "System.IO.Abstractions" remains.
        var referenced = AppAssembly.GetReferencedAssemblies()
            .Select(a => a.Name)
            .ToArray();
        await Assert.That(referenced).DoesNotContain("System.IO.Abstractions");
    }
}
