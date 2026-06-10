using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;

namespace PLang.Tests.App.FileSystem;

public class DoubleSlashIoTest
{
    [Test] public async Task DotnetIo_DoubleSlash_WritesToRealOsPath()
    {
        var realPath = "/tmp/plang-dotnet-test-" + System.Guid.NewGuid().ToString("N")[..8] + ".txt";
        var doubleSlash = "/" + realPath; // gives "//tmp/..."

        await System.IO.File.WriteAllTextAsync(doubleSlash, "double-slash-content");

        await Assert.That(System.IO.File.Exists(realPath)).IsTrue();
        var content = await System.IO.File.ReadAllTextAsync(realPath);
        await Assert.That(content).IsEqualTo("double-slash-content");

        System.IO.File.Delete(realPath);
    }
}
