using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using FilePath = global::app.type.item.path.file.@this;
using HttpPath = global::app.type.item.path.http.@this;
using Path = global::app.type.item.path.@this;

namespace PLang.Tests.App.Types.PathTests.DerivationTests;

/// <summary>
/// Path derivation verbs.
///
/// Verbs added to <c>app.type.item.path.@this</c>: <c>Parent</c>, <c>WithName</c>,
/// <c>WithExtension</c>, <c>Combine</c>, <c>InFolder</c>. Pure derivations —
/// no IO, no async, no AuthGate. Derived path inherits Context and scheme.
/// </summary>
public class PathDerivationVerbTests
{
    private static (global::app.@this app, FilePath path) FileAt(string relUnderRoot)
    {
        var root = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "plang-deriv-" + System.Guid.NewGuid().ToString("N"));
        System.IO.Directory.CreateDirectory(root);
        var app = TestApp.Create(root);
        var abs = System.IO.Path.Combine(root, relUnderRoot.TrimStart('/'));
        return (app, new FilePath(abs, app.User.Context));
    }

    private static HttpPath Http(string url)
    {
        // HttpPath only needs Context to be non-null for derivation tests that
        // check inheritance; an App with arbitrary root is fine.
        var root = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "plang-deriv-" + System.Guid.NewGuid().ToString("N"));
        System.IO.Directory.CreateDirectory(root);
        var app = TestApp.Create(root);
        return new HttpPath(url, app.User.Context);
    }

    [Test] public async Task Parent_OfFileInDirectory_ReturnsContainingDirectory()
    {
        var (_, p) = FileAt("Cache/Start.goal");
        var parent = p.Parent;
        await Assert.That(parent.Absolute).EndsWith("Cache");
    }

    [Test] public async Task Parent_OfRoot_ReturnsRootOrNullDeterministically()
    {
        var sep = System.IO.Path.DirectorySeparatorChar;
        var root = sep.ToString();
        var p = new FilePath(root, global::PLang.Tests.TestApp.SharedContext);
        var parent = p.Parent;
        // Root returns itself — no further parent, never throws.
        await Assert.That(parent.Absolute).IsEqualTo(root);
    }

    [Test] public async Task WithName_ReplacesFileNameAndKeepsDirectory()
    {
        var (_, p) = FileAt("Cache/Start.goal");
        var renamed = p.WithName("Other.goal");
        await Assert.That(renamed.Absolute).EndsWith("Cache" + System.IO.Path.DirectorySeparatorChar + "Other.goal");
    }

    [Test] public async Task WithExtension_SwapsExtensionInPlace_PureTransformation()
    {
        var (_, p) = FileAt("Cache/Start.goal");
        var pr = p.WithExtension(".pr");
        await Assert.That(pr.Absolute).EndsWith("Start.pr");
    }

    [Test] public async Task WithExtension_OnFileWithoutExtension_AddsExtension()
    {
        var (_, p) = FileAt("foo");
        var txt = p.WithExtension(".txt");
        await Assert.That(txt.Absolute).EndsWith("foo.txt");
    }

    [Test] public async Task Combine_AppendsChildSegment()
    {
        var (_, p) = FileAt("Cache");
        var child = p.Combine("Start.goal");
        await Assert.That(child.Absolute).EndsWith("Cache" + System.IO.Path.DirectorySeparatorChar + "Start.goal");
    }

    [Test] public async Task InFolder_InsertsSiblingFolderBetweenParentAndFile()
    {
        var (_, p) = FileAt("Cache/Start.goal");
        var build = p.InFolder(".build");
        var sep = System.IO.Path.DirectorySeparatorChar;
        await Assert.That(build.Absolute).EndsWith("Cache" + sep + ".build" + sep + "Start.goal");
    }

    [Test] public async Task DerivedPath_InheritsContext_FromSource()
    {
        var (app, p) = FileAt("Cache/Start.goal");
        var child = p.Combine("nested");
        await Assert.That(child.Context).IsEqualTo(app.User.Context);
    }

    [Test] public async Task DerivedPath_OnFilePath_StaysFilePath_ViaSchemeRegistry()
    {
        var (_, p) = FileAt("Cache/Start.goal");
        var parent = p.Parent;
        await Assert.That(parent is FilePath).IsTrue();
    }

    [Test] public async Task DerivedPath_OnHttpPath_StaysHttpPath_NoSilentSchemeSwitch()
    {
        var p = Http("https://example.com/a/b/c");
        var parent = p.Parent;
        await Assert.That(parent is HttpPath).IsTrue();
        await Assert.That(parent.Absolute).IsEqualTo("https://example.com/a/b");
    }
}
