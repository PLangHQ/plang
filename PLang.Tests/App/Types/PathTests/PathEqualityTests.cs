using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;

namespace PLang.Tests.App.Types.PathTests;

/// <summary>
/// <c>path.Equals</c> and <c>path.GetHashCode</c> honour
/// <see cref="@this.RootComparison"/> (Ordinal on Linux, OrdinalIgnoreCase
/// on Windows). A hard-coded <c>OrdinalIgnoreCase</c> would make
/// <c>/srv/x</c> and <c>/SRV/x</c> — distinct files on Linux — compare
/// equal and hash-collide; these tests go red on Linux if that regresses.
/// </summary>
public class PathEqualityTests
{
    private static (global::app.@this app, string root) MakeApp(string tag)
    {
        var root = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            "plang-eq-" + tag + "-" + System.Guid.NewGuid().ToString("N"));
        System.IO.Directory.CreateDirectory(root);
        return (new global::app.@this(root), root);
    }

    private static global::app.type.path.file.@this Make(global::app.@this app, string abs)
        => new(abs, app.User.Context);

    [Test] public async Task FilePath_SameAbsolute_EqualsTrue_HashEqual()
    {
        var (app, root) = MakeApp("same");
        var abs = System.IO.Path.Combine(root, "a.txt");
        var p1 = Make(app, abs);
        var p2 = Make(app, abs);
        await Assert.That(p1.Equals(p2)).IsTrue();
        await Assert.That(p1.GetHashCode()).IsEqualTo(p2.GetHashCode());
    }

    [Test] public async Task FilePath_DifferentAbsolute_EqualsFalse()
    {
        var (app, root) = MakeApp("diff");
        var p1 = Make(app, System.IO.Path.Combine(root, "a.txt"));
        var p2 = Make(app, System.IO.Path.Combine(root, "b.txt"));
        await Assert.That(p1.Equals(p2)).IsFalse();
    }

    [Test] public async Task FilePath_CaseVariant_HonoursRootComparison()
    {
        // Case sensitivity must follow the OS's filesystem rule, not a
        // hard-coded OrdinalIgnoreCase.
        var (app, root) = MakeApp("case");
        var lower = Make(app, System.IO.Path.Combine(root, "casefile.txt"));
        var upper = Make(app, System.IO.Path.Combine(root, "CASEFILE.TXT"));

        if (System.OperatingSystem.IsWindows())
        {
            await Assert.That(lower.Equals(upper)).IsTrue();
            await Assert.That(lower.GetHashCode()).IsEqualTo(upper.GetHashCode());
        }
        else
        {
            // Linux: distinct files — must compare unequal.
            await Assert.That(lower.Equals(upper)).IsFalse();
            // Ordinal hashes of the two distinct strings differ in practice — the
            // strict equality contract only requires Equals==true ⇒ hash equal,
            // but a hash collision here would indicate the override still uses
            // OrdinalIgnoreCase.
            await Assert.That(lower.GetHashCode()).IsNotEqualTo(upper.GetHashCode());
        }
    }

    [Test] public async Task FilePath_EqualsString_ComparesAbsolute()
    {
        var (app, root) = MakeApp("estr");
        var abs = System.IO.Path.Combine(root, "x.txt");
        var p = Make(app, abs);

        await Assert.That(p.Equals(abs)).IsTrue();

        var caseFlipped = abs.Replace("x.txt", "X.TXT");
        if (System.OperatingSystem.IsWindows())
            await Assert.That(p.Equals(caseFlipped)).IsTrue();
        else
            await Assert.That(p.Equals(caseFlipped)).IsFalse();
    }
}
