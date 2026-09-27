namespace PLang.Tests.App.Docs;

/// <summary>
/// A folder's docs are its <c>start.md</c>, as <c>Start.goal</c> is its entry. Where GitHub or tools
/// expect a <c>README.md</c>, it is a copy of the <c>start.md</c> beside it: <c>start.md</c> is the one
/// edited, and the two may not drift.
/// </summary>
public class StartMdTests
{
    private static string RepoRoot()
    {
        var dir = System.AppContext.BaseDirectory;
        while (dir != null && !System.IO.Directory.Exists(System.IO.Path.Combine(dir, "PLang", "app")))
            dir = System.IO.Directory.GetParent(dir)?.FullName;
        return dir!;
    }

    [Test]
    [Arguments(".")]
    [Arguments("PLang")]
    [Arguments("PlangConsole")]
    [Arguments("Skill")]
    [Arguments("tools/decider")]
    public async Task ReadmeIsACopyOfStartMd(string folder)
    {
        var dir = System.IO.Path.Combine(RepoRoot(), folder);
        var start = await System.IO.File.ReadAllTextAsync(System.IO.Path.Combine(dir, "start.md"));
        var readme = await System.IO.File.ReadAllTextAsync(System.IO.Path.Combine(dir, "README.md"));
        await Assert.That(readme).IsEqualTo(start)
            .Because($"{folder}/README.md is a copy of {folder}/start.md — edit start.md, then copy it over README.md");
    }
}
