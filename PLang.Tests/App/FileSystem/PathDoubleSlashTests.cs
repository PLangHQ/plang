using Path = global::app.type.path.file.@this;
using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;

namespace PLang.Tests.App.FileSystem;

/// Verifies //tmp/X resolves to a real OS-rooted absolute path that survives
/// the v1 file IO wrap (PLangFile.ValidatePath × N).
public class PathDoubleSlashTests
{
    [Test] public async Task DoubleSlash_PathResolve_Absolute_IsPreserved()
    {
        var root = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            "plang-pds-" + System.Guid.NewGuid().ToString("N")[..8]);
        System.IO.Directory.CreateDirectory(root);
        var app = new global::app.@this(root);
        var path = Path.Resolve("//tmp/plang-ds-test.txt", app.User.Context);
        await Assert.That(path.Absolute).IsEqualTo("//tmp/plang-ds-test.txt");
    }

    [Test] public async Task DoubleSlash_FsFile_WriteThenRead_RoundTrips()
    {
        var root = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            "plang-pds-" + System.Guid.NewGuid().ToString("N")[..8]);
        System.IO.Directory.CreateDirectory(root);
        var app = new global::app.@this(root);
        var realPath = "/tmp/plang-ds-test-" + System.Guid.NewGuid().ToString("N")[..8] + ".txt";
        var doubleSlash = "/" + realPath;
        await System.IO.File.WriteAllTextAsync(doubleSlash, "fs-content");
        var existsAtReal = System.IO.File.Exists(realPath);
        await Assert.That(existsAtReal).IsTrue();
        var content = await System.IO.File.ReadAllTextAsync(doubleSlash);
        await Assert.That(content).IsEqualTo("fs-content");
        System.IO.File.Delete(realPath);
    }
}
