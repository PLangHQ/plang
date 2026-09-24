using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using System.Reflection;
using PLangPath = global::app.type.item.path.@this;
using FilePath = global::app.type.item.path.file.@this;

namespace PLang.Tests.App.Types.PathTests;

/// <summary>
/// Shape tests: the path base is abstract; FilePath is the concrete
/// file-scheme subclass; verb surface declared abstract on base; CopyTo/MoveTo
/// virtual (cross-scheme default).
/// </summary>
public class PathAbstractTests
{
    // The abstract verb surface — every verb's last parameter is the caller's context.
    // Delete/List/Save carry the option-bearing signatures lifted onto the base; the
    // context-only Delete(context)/List(context) are non-abstract convenience and are not listed here.
    private static readonly System.Type Ctx = typeof(global::app.actor.context.@this);
    private static readonly (string Name, System.Type[] Params)[] AbstractVerbs =
    {
        (nameof(PLangPath.ReadText), new[] { Ctx }),
        (nameof(PLangPath.ReadBytes), new[] { Ctx }),
        (nameof(PLangPath.WriteText), new[] { typeof(string), Ctx }),
        (nameof(PLangPath.WriteBytes), new[] { typeof(byte[]), Ctx }),
        (nameof(PLangPath.Append), new[] { typeof(string), Ctx }),
        (nameof(PLangPath.ExistsAsync), new[] { Ctx }),
        (nameof(PLangPath.Stat), new[] { Ctx }),
        (nameof(PLangPath.Mkdir), new[] { Ctx }),
        (nameof(PLangPath.AsBooleanAsync), new[] { Ctx }),
        (nameof(PLangPath.Delete), new[] { typeof(bool), typeof(bool), Ctx }),
        (nameof(PLangPath.List), new[] { typeof(string), typeof(bool), Ctx }),
        (nameof(PLangPath.Save), new[] { typeof(global::app.data.@this), Ctx }),
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
        foreach (var (name, paramTypes) in AbstractVerbs)
        {
            var m = typeof(PLangPath).GetMethod(
                name, BindingFlags.Public | BindingFlags.Instance, binder: null, paramTypes, modifiers: null);
            await Assert.That(m).IsNotNull();
            await Assert.That(m!.IsAbstract).IsTrue();
        }
    }

    [Test] public async Task Path_Delete_List_ContextOnly_AreConvenience_NotAbstract()
    {
        foreach (var name in new[] { nameof(PLangPath.Delete), nameof(PLangPath.List) })
        {
            var m = typeof(PLangPath).GetMethod(
                name, BindingFlags.Public | BindingFlags.Instance, binder: null, new[] { Ctx }, modifiers: null);
            await Assert.That(m).IsNotNull();
            await Assert.That(m!.IsAbstract).IsFalse();
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
