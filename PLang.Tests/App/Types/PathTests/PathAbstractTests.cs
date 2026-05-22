using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using System.Reflection;
using PLangPath = global::app.types.path.@this;
using FilePath = global::app.types.path.file.@this;

namespace PLang.Tests.App.Types.PathTests;

/// <summary>
/// Stage 2 — shape tests: the path base is abstract; FilePath is the concrete
/// file-scheme subclass; verb surface declared abstract on base; CopyTo/MoveTo
/// virtual (cross-scheme default).
/// </summary>
public class PathAbstractTests
{
    private static readonly string[] AbstractVerbs =
    {
        nameof(PLangPath.ReadText), nameof(PLangPath.ReadBytes), nameof(PLangPath.WriteText),
        nameof(PLangPath.WriteBytes), nameof(PLangPath.Append), nameof(PLangPath.Delete),
        nameof(PLangPath.ExistsAsync), nameof(PLangPath.Stat), nameof(PLangPath.List),
    };

    [Test] public async Task Path_IsAbstract_CannotInstantiateDirectly()
    {
        await Assert.That(typeof(PLangPath).IsAbstract).IsTrue();
    }

    [Test] public async Task FilePath_DerivesFrom_PathBase()
    {
        await Assert.That(typeof(FilePath).IsSubclassOf(typeof(PLangPath))).IsTrue();
    }

    [Test] public async Task Path_DeclaresAbstract_VerbSurface()
    {
        foreach (var name in AbstractVerbs)
        {
            var m = typeof(PLangPath).GetMethod(name, BindingFlags.Public | BindingFlags.Instance);
            await Assert.That(m).IsNotNull();
            await Assert.That(m!.IsAbstract).IsTrue();
        }
    }

    [Test] public async Task Path_CopyTo_MoveTo_AreVirtual_WithBaseDefault()
    {
        foreach (var name in new[] { nameof(PLangPath.CopyTo), nameof(PLangPath.MoveTo) })
        {
            var m = typeof(PLangPath).GetMethod(name, BindingFlags.Public | BindingFlags.Instance);
            await Assert.That(m).IsNotNull();
            await Assert.That(m!.IsVirtual).IsTrue();
            await Assert.That(m.IsAbstract).IsFalse();
        }
    }

    [Test] public async Task FilePath_Scheme_IsFile_And_Raw_RoundTrips()
    {
        var p = new FilePath("/tmp/x.txt") { Raw = "/tmp/x.txt" };
        await Assert.That(p.Scheme).IsEqualTo("file");
        await Assert.That(p.Raw).IsEqualTo("/tmp/x.txt");
    }

    [Test] public async Task Authorize_StaysOnBase_NotPerSchemeOverride()
    {
        var m = typeof(PLangPath).GetMethod(nameof(PLangPath.Authorize), BindingFlags.Public | BindingFlags.Instance);
        await Assert.That(m).IsNotNull();
        await Assert.That(m!.DeclaringType).IsEqualTo(typeof(PLangPath));
        // FilePath must not declare its own Authorize override.
        var derived = typeof(FilePath).GetMethod(nameof(PLangPath.Authorize),
            BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
        await Assert.That(derived).IsNull();
    }
}
