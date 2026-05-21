using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;

namespace PLang.Tests.App.FileSystem;

/// Pins the three changes to PLangFileSystem.ValidatePath that landed with
/// the filesystem-permission branch:
///   1. IsOsRooted check runs BEFORE IsPlangRooted — needed because both
///      Linux forms start with "/", and the order originally made the
///      OS-rooted branch dead code.
///   2. The "//" prefix is preserved (no Substring strip) so subsequent
///      ValidatePath wrappers (PLangFile.X re-validates) don't mistake
///      "/tmp/X" for plang-rooted and re-prefix RootDirectory.
///   3. Paths outside RootDirectory no longer throw
///      UnauthorizedAccessException. Gating is Path.Authorize's job now;
///      ValidatePath only normalises.
public class ValidatePathTests
{
    private static global::App.@this NewApp(out string root)
    {
        root = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            "plang-vp-" + System.Guid.NewGuid().ToString("N")[..8]);
        System.IO.Directory.CreateDirectory(root);
        return new global::App.@this(root);
    }

    [Test] public async Task PlangRootedPath_GetsRootPrefix()
    {
        var app = NewApp(out var root);
        var resolved = app.FileSystem.ValidatePath("/data/file.txt");
        // /data/file.txt → prefixed with root → <root>/data/file.txt
        await Assert.That(resolved.StartsWith(root)).IsTrue();
        await Assert.That(resolved.EndsWith("data/file.txt") || resolved.EndsWith("data\\file.txt")).IsTrue();
    }

    [Test] public async Task OsRootedPath_PreservedAsIs_UnixDoubleSlash()
    {
        if (!OperatingSystem.IsLinux() && !OperatingSystem.IsMacOS()) return; // Unix form only
        var app = NewApp(out _);
        var resolved = app.FileSystem.ValidatePath("//tmp/foo.txt");
        // // form must NOT be stripped — preserved so re-validation stays idempotent.
        await Assert.That(resolved).IsEqualTo("//tmp/foo.txt");
    }

    [Test] public async Task OsRootedPath_DoubleValidate_StillOsRooted()
    {
        if (!OperatingSystem.IsLinux() && !OperatingSystem.IsMacOS()) return;
        var app = NewApp(out _);
        var first = app.FileSystem.ValidatePath("//tmp/foo.txt");
        var second = app.FileSystem.ValidatePath(first);
        // PLangFile wraps every File.X call with another ValidatePath; the
        // second pass must NOT re-prefix RootDirectory.
        await Assert.That(second).IsEqualTo(first);
    }

    [Test] public async Task OutOfRootPath_DoesNotThrow()
    {
        var app = NewApp(out _);
        // Previously this threw UnauthorizedAccessException via the
        // fileAccesses gate. Now ValidatePath is just a normaliser;
        // Permission.Authorize handles gating at the action-handler level.
        var resolved = app.FileSystem.ValidatePath("//tmp/elsewhere.txt");
        await Assert.That(resolved).IsNotNull();
    }

    [Test] public async Task RelativePath_GetsRootPrefix()
    {
        var app = NewApp(out var root);
        var resolved = app.FileSystem.ValidatePath("data/file.txt");
        await Assert.That(resolved.StartsWith(root)).IsTrue();
    }

    /// Regression: codeanalyzer v2 #2. On case-sensitive filesystems an
    /// upper-cased copy of the root prefix is treated as a *new* plang-rooted
    /// path and gets re-prefixed under the real (lowercase) root — it must
    /// NOT be silently merged into the root as if it were already in-root.
    ///
    /// The security-observable gate test lives in PathAuthorizeTests
    /// (`IsInRoot_UpperCasedRoot_TreatedAsOutOfRoot_OnUnix`) — that's where
    /// the case comparison actually decides whether to auto-grant. This pin
    /// catches a regression where line 191 of ValidatePath flipped to
    /// case-insensitive (which would let `/TMP/X/f` slip through unprefixed
    /// and then the `StartsWith(RootDirectory)` check at line 227 would treat
    /// it as in-root).
    [Test] public async Task UpperCasedRootPrefix_TreatedAsNewPath_AndRePrefixed_OnUnix()
    {
        if (!OperatingSystem.IsLinux() && !OperatingSystem.IsMacOS()) return;
        var app = NewApp(out var root);
        var uppered = root.ToUpperInvariant() + "/file.txt";
        var resolved = app.FileSystem.ValidatePath(uppered);
        // Observed contract: the upper-cased version is unrecognised as the
        // current root and ValidatePath joins it onto the real root.
        await Assert.That(resolved.StartsWith(root)).IsTrue();
        await Assert.That(resolved).IsEqualTo(System.IO.Path.Join(root, uppered));
    }

    [Test] public async Task InRootAbsolute_LeftAlone()
    {
        var app = NewApp(out var root);
        var inRoot = System.IO.Path.Combine(root, "subdir", "file.txt");
        var resolved = app.FileSystem.ValidatePath(inRoot);
        await Assert.That(resolved).IsEqualTo(inRoot);
    }
}
