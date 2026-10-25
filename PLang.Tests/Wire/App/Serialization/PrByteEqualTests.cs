namespace PLang.Tests.App.Serialization;

// A goal .pr reads the way the runtime reads one off disk and writes back the way the builder writes one. Once
// written by today's writer, reading and writing it again gives the same bytes. A .pr that no longer reads (an
// outdated format waiting for its rebuild) is its own answer, not a byte change.
public class PrByteEqualTests
{
    [Test]
    public async Task EveryBuiltPr_WrittenOnce_ReadsAndWritesBackTheSameBytes()
    {
        await using var app = new global::app.@this(Root, global::PLang.Tests.Parent.Actor).Testing();
        var context = app.actor.list.User.Context;
        var changed = new List<string>();
        foreach (var (relative, text) in Prs())
        {
            if (await Read(app, text, relative) is not { } goal) continue;
            var once = await context.Pr(goal);
            var twice = await context.Pr((await Read(app, once, relative))!);
            if (twice != once) changed.Add($"{relative}: {FirstDifference(once, twice)}");
        }
        await Assert.That(string.Join("\n", changed.Take(25))).IsEqualTo("");
    }

    // The .pr files on disk against today's writer: what a cached rebuild would rewrite. Run by hand.
    [Test, Explicit]
    public async Task EveryBuiltPr_OnDisk_IsWhatTodaysWriterWrites()
    {
        await using var app = new global::app.@this(Root, global::PLang.Tests.Parent.Actor).Testing();
        var context = app.actor.list.User.Context;
        var changed = new List<string>();
        foreach (var (relative, text) in Prs())
            if (await Read(app, text, relative) is { } goal && await context.Pr(goal) is var written && written != text)
                changed.Add($"{relative}: {FirstDifference(text, written)}");
        System.IO.File.WriteAllText(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "pr-byte-equal.txt"), string.Join("\n\n", changed));
        await Assert.That(changed.Count).IsEqualTo(0);
    }

    private static string Root => System.IO.Path.Combine(Fixture.Root(), "test");

    // Every goal .pr under the test tree, by its path from the tree's root.
    private static IEnumerable<(string Relative, string Text)> Prs()
        => System.IO.Directory.EnumerateFiles(Root, "*.pr", System.IO.SearchOption.AllDirectories)
            .Where(file => System.IO.Path.GetFileName(file) != "app.pr")
            .Select(file => ("/" + System.IO.Path.GetRelativePath(Root, file).Replace('\\', '/'), System.IO.File.ReadAllText(file)));

    // The goal a .pr's text is, read as the file at relative is; null when the read refuses it.
    private static async Task<global::app.goal.@this?> Read(global::app.@this app, string text, string relative)
    {
        try
        {
            return await global::PLang.Tests.Shared.RealGoalLoad.Read(app, text,
                global::app.type.item.path.@this.Resolve(relative, app.actor.list.User.Context));
        }
        catch (System.Exception ex) when (ex is System.InvalidOperationException or global::app.error.AppException)
        {
            return null;
        }
    }

    // Where the second text leaves the first: the 60 characters around the first one that differs.
    private static string FirstDifference(string first, string second)
    {
        var at = 0;
        while (at < first.Length && at < second.Length && first[at] == second[at]) at++;
        string around(string s) => s.Substring(System.Math.Max(0, at - 30), System.Math.Min(60, s.Length - System.Math.Max(0, at - 30)));
        return $"at {at}: …{around(first)}… then …{around(second)}…";
    }
}
