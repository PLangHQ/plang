using System.Text.RegularExpressions;

namespace PLang.Tests.App.FileSystem.PermissionTests;

/// <summary>Security note (windows, S-W2): a glob grant on Windows is a Regex with IgnoreCase. This pins what .NET's regex
/// folding does beyond what NTFS folds, so a change shows. Informational: it only widens a glob over names a user created
/// under a granted prefix.</summary>
public class GlobCaseFoldProbeTests
{
    [Test]
    public async Task RegexIgnoreCase_FoldsTheKelvinSign_NtfsDoesNot()
    {
        var kelvin = "K";
        var matches = Regex.IsMatch("/apps/" + kelvin + "dir/x", "^/apps/kdir/x$", RegexOptions.IgnoreCase);
        var ordinal = string.Equals("/apps/" + kelvin + "dir/x", "/apps/kdir/x", System.StringComparison.OrdinalIgnoreCase);
        System.Console.Error.WriteLine($"PROBE regex folds kelvin={matches}, OrdinalIgnoreCase folds kelvin={ordinal}");
        await Assert.That(ordinal).IsFalse();     // the exact comparison: not folded
        await Assert.That(matches).IsFalse();     // the glob: SAFE would be the same as the exact comparison
    }
}
