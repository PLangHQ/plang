using System.Diagnostics;

namespace PLang.Tests.App;

/// <summary>A build leaves &lt;OutDir&gt;/os a link to the repo's os tree (os.targets): a stale plain folder there is
/// replaced, a link is never touched. A plain copy would read old system goals and teaching for no code reason.</summary>
public class OsLinkTests
{
    private static string Repo()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "os.targets"))) dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("os.targets not found above " + AppContext.BaseDirectory);
    }

    // a project of its own that only imports os.targets, built into a bin the test owns
    private static async Task<string> Build(string root)
    {
        var bin = Path.Combine(root, "bin") + Path.DirectorySeparatorChar;
        Directory.CreateDirectory(bin);
        var project = Path.Combine(root, "probe.proj");
        await File.WriteAllTextAsync(project,
            $"<Project><Import Project=\"{Path.Combine(Repo(), "os.targets")}\" /><Target Name=\"Build\" /></Project>");
        var start = new ProcessStartInfo("dotnet", ["msbuild", project, "-t:Build", "-nologo", "-p:OutDir=" + bin])
        {
            RedirectStandardOutput = true, RedirectStandardError = true, RedirectStandardInput = true,
        };
        using var msbuild = Process.Start(start)!;
        msbuild.StandardInput.Close();
        var output = await msbuild.StandardOutput.ReadToEndAsync();
        await msbuild.WaitForExitAsync();
        if (msbuild.ExitCode != 0) throw new InvalidOperationException("msbuild failed: " + output);
        return Path.Combine(bin, "os");
    }

    private static string Scratch()
    {
        var root = Path.Combine(Path.GetTempPath(), "oslink_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }

    [Test]
    public async Task AStalePlainOs_IsReplacedByTheLink()
    {
        var root = Scratch();
        try
        {
            var stale = Path.Combine(root, "bin", "os", "system");
            Directory.CreateDirectory(stale);
            await File.WriteAllTextAsync(Path.Combine(stale, "old.txt"), "an older build's copy");

            var os = await Build(root);

            await Assert.That(new DirectoryInfo(os).LinkTarget).IsEqualTo(Path.Combine(Repo(), "os"));
            await Assert.That(File.Exists(Path.Combine(os, "system", "old.txt"))).IsFalse();
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Test]
    public async Task ALink_IsLeftAsItIs()
    {
        var root = Scratch();
        try
        {
            var elsewhere = Path.Combine(root, "elsewhere");
            Directory.CreateDirectory(elsewhere);
            await File.WriteAllTextAsync(Path.Combine(elsewhere, "kept.txt"), "behind a link");
            Directory.CreateDirectory(Path.Combine(root, "bin"));
            Directory.CreateSymbolicLink(Path.Combine(root, "bin", "os"), elsewhere);

            var os = await Build(root);

            await Assert.That(new DirectoryInfo(os).LinkTarget).IsEqualTo(elsewhere);
            await Assert.That(File.Exists(Path.Combine(elsewhere, "kept.txt"))).IsTrue();
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Test]
    public async Task NoOs_IsLinked()
    {
        var root = Scratch();
        try
        {
            var os = await Build(root);

            await Assert.That(new DirectoryInfo(os).LinkTarget).IsEqualTo(Path.Combine(Repo(), "os"));
        }
        finally { Directory.Delete(root, recursive: true); }
    }
}
