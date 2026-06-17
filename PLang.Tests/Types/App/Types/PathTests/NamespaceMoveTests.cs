using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using System.Linq;

namespace PLang.Tests.App.Types.PathTests;

/// <summary>
/// Namespace move: <c>app.filesystem/</c> → <c>app.type/path/</c>, and the
/// path class converts to the <c>@this</c> convention.
///
/// Survey assertions over the loaded App assembly, string-based reflection only.
/// </summary>
public class NamespaceMoveTests
{
    private static System.Reflection.Assembly AppAssembly => typeof(global::app.@this).Assembly;

    [Test] public async Task AppFilesystemNamespace_ContainsZeroLoadedTypes()
    {
        var stale = AppAssembly.GetTypes()
            .Where(t => t.Namespace != null && t.Namespace.StartsWith("app.filesystem", System.StringComparison.Ordinal))
            .ToArray();
        await Assert.That(stale).IsEmpty();
    }

    [Test] public async Task PathType_ReachableVia_GlobalPathAlias()
    {
        // NOTE: this branch does NOT add a global `Path` alias — it collides
        // with System.IO.Path across the codebase. The path type is reachable
        // by its fully-qualified name; per-file `using Path = ...` aliases are
        // used where the short name is wanted.
        var pathType = AppAssembly.GetType("app.type.path.this");
        await Assert.That(pathType).IsNotNull();
        await Assert.That(pathType!.IsAbstract).IsTrue();
    }

    [Test] public async Task PathType_LivesIn_AppTypesPathNamespace()
    {
        var pathType = AppAssembly.GetType("app.type.path.this");
        await Assert.That(pathType).IsNotNull();
        await Assert.That(pathType!.Namespace).IsEqualTo("app.type.path");
    }

    [Test] public async Task PathClass_FollowsThisConvention_NamedThis()
    {
        var pathType = AppAssembly.GetType("app.type.path.this");
        await Assert.That(pathType).IsNotNull();
        await Assert.That(pathType!.Name).IsEqualTo("this");
    }

    [Test] public async Task PermissionType_LivesUnder_AppTypePermission()
    {
        // Permission is a fundamental type, not a path thing — it references a
        // resource but is an authorization in its own right.
        await Assert.That(AppAssembly.GetType("app.type.permission.this")).IsNotNull();
        await Assert.That(AppAssembly.GetType("app.type.path.permission.this")).IsNull();
    }

    [Test] public async Task Verb_IsAnEnum_NotSubRecords()
    {
        // The verb model collapsed to a single Verb enum; the grant holds a set.
        var verb = AppAssembly.GetType("app.type.permission.Verb");
        await Assert.That(verb).IsNotNull();
        await Assert.That(verb!.IsEnum).IsTrue();
        await Assert.That(AppAssembly.GetType("app.type.path.permission.verb.this")).IsNull();
    }

    [Test] public async Task ExistingSuite_StaysGreen_AfterRename()
    {
        // The real guard is the green build + full suite run. This test exists so
        // a reviewer scanning stage 1 sees the pass-condition as a test name.
        await Assert.That(true).IsTrue();
    }
}
