using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using global::app.type.path.scheme;
using PLangPath = global::app.type.path.@this;
using FilePath = global::app.type.path.file.@this;

namespace PLang.Tests.App.Types.PathTests;

/// <summary>
/// The per-App scheme registry (<c>app.type.path.scheme.@this</c>), reachable as
/// <c>app.Type.Scheme</c>.
/// </summary>
public class SchemeRegistryTests
{
    private static (global::app.@this app, global::app.actor.context.@this context) MakeApp()
    {
        var dir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "plang-scheme-" + System.Guid.NewGuid().ToString("N"));
        System.IO.Directory.CreateDirectory(dir);
        var app = new global::app.@this(dir);
        return (app, app.User.Context);
    }

    [Test] public async Task Register_ThenFrom_ReturnsRegisteredSubclass()
    {
        var (app, context) = MakeApp();
        app.Type.Scheme.Register("test", (raw, c) => new FilePath(raw, c) { Raw = raw });
        var p = app.Type.Scheme.From("test://hello", context);
        await Assert.That(p).IsNotNull();
        await Assert.That(p is FilePath).IsTrue();
    }

    [Test] public async Task Register_SameSchemeTwice_SecondRegistrationReplacesFirst()
    {
        var (app, context) = MakeApp();
        var first = new FilePath("/first", context);
        var second = new FilePath("/second", context);
        app.Type.Scheme.Register("dup", (raw, c) => first);
        app.Type.Scheme.Register("dup", (raw, c) => second);
        var p = app.Type.Scheme.From("dup://x", context);
        await Assert.That(object.ReferenceEquals(p, second)).IsTrue();
    }

    [Test] public async Task From_BareAbsolutePath_RoutesToFilePath()
    {
        var (app, context) = MakeApp();
        var p = app.Type.Scheme.From("/tmp/anywhere/x.txt", context);
        await Assert.That(p is FilePath).IsTrue();
        await Assert.That(p.Scheme).IsEqualTo("file");
    }

    [Test] public async Task From_BareRelativePath_RoutesToFilePath()
    {
        var (app, context) = MakeApp();
        var p = app.Type.Scheme.From("relative.txt", context);
        await Assert.That(p is FilePath).IsTrue();
    }

    [Test] public async Task From_WindowsDriveLetterPath_RoutesToFilePath_NotSchemeColon()
    {
        var (app, context) = MakeApp();
        // C:\... has a colon but is NOT "scheme://" — must not be treated as a scheme.
        var p = app.Type.Scheme.From("C:\\Users\\x.txt", context);
        await Assert.That(p is FilePath).IsTrue();
    }

    [Test] public async Task From_ExplicitFileScheme_RoutesToFilePath()
    {
        var (app, context) = MakeApp();
        var p = app.Type.Scheme.From("file:///home/user/x.txt", context);
        await Assert.That(p is FilePath).IsTrue();
        await Assert.That(p.Scheme).IsEqualTo("file");
    }

    [Test] public async Task From_SchemeMatching_IsCaseInsensitive()
    {
        var (app, context) = MakeApp();
        // Built-in "file" registered lowercase; FILE:// must resolve to it.
        var p = app.Type.Scheme.From("FILE:///x.txt", context);
        await Assert.That(p is FilePath).IsTrue();
    }

    [Test] public async Task From_UnknownScheme_ThrowsTypedSchemeNotRegistered()
    {
        var (app, context) = MakeApp();
        var ex = await Assert.That(() => app.Type.Scheme.From("s3://bucket/key", context)).Throws<SchemeNotRegistered>();
        await Assert.That(ex!.Scheme).IsEqualTo("s3");
    }

    [Test] public async Task MultiApp_Registrations_AreIsolated()
    {
        var (a, _) = MakeApp();
        var (b, ctxB) = MakeApp();
        a.Type.Scheme.Register("zzz", (raw, c) => new FilePath(raw, c));
        await Assert.That(a.Type.Scheme.IsRegistered("zzz")).IsTrue();
        await Assert.That(b.Type.Scheme.IsRegistered("zzz")).IsFalse();
        await Assert.That(() => b.Type.Scheme.From("zzz://x", ctxB)).Throws<SchemeNotRegistered>();
    }

    [Test] public async Task SchemeRegistry_ExposedAt_AppTypesScheme()
    {
        var (app, _) = MakeApp();
        var r1 = app.Type.Scheme;
        var r2 = app.Type.Scheme;
        await Assert.That(r1).IsNotNull();
        await Assert.That(object.ReferenceEquals(r1, r2)).IsTrue();
    }
}
