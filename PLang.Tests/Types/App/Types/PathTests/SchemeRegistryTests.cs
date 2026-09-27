using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using global::app.type.item.path.scheme;
using PLangPath = global::app.type.item.path.@this;
using FilePath = global::app.type.item.path.file.@this;
using SchemeKind = global::app.type.item.path.scheme.@this;

namespace PLang.Tests.App.Types.PathTests;

/// <summary>
/// Path schemes are kinds of path, held per App in its type list (<c>app.type.list.Add</c>, <c>app.type.list.Kind(name)</c>);
/// <c>path.Resolve</c> routes a raw path through the scheme kind it names.
/// </summary>
public class SchemeRegistryTests
{
    private static (global::app.@this app, global::app.actor.context.@this context) MakeApp()
    {
        var dir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "plang-scheme-" + System.Guid.NewGuid().ToString("N"));
        System.IO.Directory.CreateDirectory(dir);
        var app = TestApp.Create(dir);
        return (app, app.User.Context);
    }

    [Test] public async Task Register_ThenFrom_ReturnsRegisteredSubclass()
    {
        var (app, context) = MakeApp();
        app.type.list.Add(new SchemeKind("test", (raw, c) => new FilePath(raw) { Raw = raw }));
        var p = PLangPath.Resolve("test://hello", context);
        await Assert.That(p).IsNotNull();
        await Assert.That(p is FilePath).IsTrue();
    }

    [Test] public async Task Register_SameSchemeTwice_SecondRegistrationReplacesFirst()
    {
        var (app, context) = MakeApp();
        var first = new FilePath("/first");
        var second = new FilePath("/second");
        app.type.list.Add(new SchemeKind("dup", (raw, c) => first));
        app.type.list.Add(new SchemeKind("dup", (raw, c) => second));
        var p = PLangPath.Resolve("dup://x", context);
        await Assert.That(object.ReferenceEquals(p, second)).IsTrue();
    }

    [Test] public async Task From_BareAbsolutePath_RoutesToFilePath()
    {
        var (app, context) = MakeApp();
        var p = PLangPath.Resolve("/tmp/anywhere/x.txt", context);
        await Assert.That(p is FilePath).IsTrue();
        await Assert.That(p.Scheme).IsEqualTo("file");
    }

    [Test] public async Task From_BareRelativePath_RoutesToFilePath()
    {
        var (app, context) = MakeApp();
        var p = PLangPath.Resolve("relative.txt", context);
        await Assert.That(p is FilePath).IsTrue();
    }

    [Test] public async Task From_WindowsDriveLetterPath_RoutesToFilePath_NotSchemeColon()
    {
        var (app, context) = MakeApp();
        // C:\... has a colon but is NOT "scheme://" — must not be treated as a scheme.
        var p = PLangPath.Resolve("C:\\Users\\x.txt", context);
        await Assert.That(p is FilePath).IsTrue();
    }

    [Test] public async Task From_ExplicitFileScheme_RoutesToFilePath()
    {
        var (app, context) = MakeApp();
        var p = PLangPath.Resolve("file:///home/user/x.txt", context);
        await Assert.That(p is FilePath).IsTrue();
        await Assert.That(p.Scheme).IsEqualTo("file");
    }

    [Test] public async Task From_SchemeMatching_IsCaseInsensitive()
    {
        var (app, context) = MakeApp();
        // Built-in "file" is lowercase; FILE:// must resolve to it.
        var p = PLangPath.Resolve("FILE:///x.txt", context);
        await Assert.That(p is FilePath).IsTrue();
    }

    [Test] public async Task From_UnknownScheme_ThrowsTypedSchemeNotRegistered()
    {
        var (app, context) = MakeApp();
        var ex = await Assert.That(() => PLangPath.Resolve("s3://bucket/key", context)).Throws<SchemeNotRegistered>();
        await Assert.That(ex!.Scheme).IsEqualTo("s3");
    }

    [Test] public async Task MultiApp_Registrations_AreIsolated()
    {
        var (a, _) = MakeApp();
        var (b, ctxB) = MakeApp();
        a.type.list.Add(new SchemeKind("zzz", (raw, c) => new FilePath(raw)));
        await Assert.That(a.type.list.Kind("zzz") is SchemeKind).IsTrue();
        await Assert.That(b.type.list.Kind("zzz") is SchemeKind).IsFalse();
        await Assert.That(() => PLangPath.Resolve("zzz://x", ctxB)).Throws<SchemeNotRegistered>();
    }

    [Test] public async Task BuiltInSchemes_AreKindsOfTheApp()
    {
        var (app, _) = MakeApp();
        await Assert.That(app.type.list.Kind("file") is SchemeKind).IsTrue();
    }
}
