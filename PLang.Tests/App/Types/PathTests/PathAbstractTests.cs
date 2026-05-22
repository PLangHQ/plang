using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;

namespace PLang.Tests.App.Types.PathTests;

/// <summary>
/// Stage 2 — <c>path</c> becomes an abstract base. Today's concrete impl moves into
/// <c>FilePath : Path</c> (<c>app.types.path.file.@this</c>). The verb surface
/// (Read/Write/Delete/Exists/Stat/List + ReadText/ReadBytes/WriteText/WriteBytes/Append)
/// is declared <c>abstract</c> on the base; <c>CopyTo</c>/<c>MoveTo</c> are <c>virtual</c>
/// with cross-scheme default bodies (ReadBytes → WriteBytes; CopyTo + Delete).
///
/// These are shape tests over the type hierarchy — reflection on the abstract base and
/// the FilePath subclass.
/// </summary>
public class PathAbstractTests
{
    /// <summary>Intent: the path base type is <c>abstract</c> — it cannot be instantiated
    /// directly. <c>typeof(Path).IsAbstract</c> is true.</summary>
    [Test] public async Task Path_IsAbstract_CannotInstantiateDirectly()
    {
        Assert.Fail("Not implemented");
    }

    /// <summary>Intent: <c>FilePath</c> derives from the abstract <c>Path</c> base
    /// (<c>typeof(FilePath).IsSubclassOf(typeof(Path))</c>).</summary>
    [Test] public async Task FilePath_DerivesFrom_PathBase()
    {
        Assert.Fail("Not implemented");
    }

    /// <summary>Intent: the verb surface is declared <c>abstract</c> on the base — each of
    /// <c>ReadText</c>, <c>ReadBytes</c>, <c>WriteText</c>, <c>WriteBytes</c>,
    /// <c>Append</c>, <c>Delete</c>, <c>Exists</c>, <c>Stat</c>, <c>List</c> is an abstract
    /// member on <c>Path</c>. (The exact set mirrors today's <c>path.Operations.cs</c>;
    /// don't invent new verbs.)</summary>
    [Test] public async Task Path_DeclaresAbstract_VerbSurface()
    {
        Assert.Fail("Not implemented");
    }

    /// <summary>Intent: <c>CopyTo</c> and <c>MoveTo</c> are <c>virtual</c> (overridable)
    /// but NOT abstract — the base provides cross-scheme default bodies so a new scheme
    /// gets file↔scheme transfer for free. Reflect <c>MethodInfo.IsVirtual &amp;&amp;
    /// !IsAbstract</c>.</summary>
    [Test] public async Task Path_CopyTo_MoveTo_AreVirtual_WithBaseDefault()
    {
        Assert.Fail("Not implemented");
    }

    /// <summary>Intent: <c>FilePath</c> overrides <c>Scheme</c> to return <c>"file"</c>,
    /// and <c>Raw</c> returns the string it was constructed with. Construct via
    /// <c>new FilePath(raw)</c>.</summary>
    [Test] public async Task FilePath_Scheme_IsFile_And_Raw_RoundTrips()
    {
        Assert.Fail("Not implemented");
    }

    /// <summary>Intent: <c>this.Authorize.cs</c> (the Permission gate) stays a
    /// non-virtual member ON the base — permission gating is scheme-agnostic.
    /// <c>FilePath</c> inherits it; it is not re-declared per scheme. Reflect that
    /// <c>Authorize</c> is declared on <c>Path</c>, not overridden on <c>FilePath</c>.</summary>
    [Test] public async Task Authorize_StaysOnBase_NotPerSchemeOverride()
    {
        Assert.Fail("Not implemented");
    }
}
