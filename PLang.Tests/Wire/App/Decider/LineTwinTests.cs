namespace PLang.Tests.App.Decider;

// The pre-filled formal line (goal/step/pick/line) against a pinned fixture (line_golden.json): each case's moves
// write the line pinned for it — a lone if holds the step's actions in its { }, a chain stays flat, a loop leads, a
// keep follows the producers. An intended change re-pins it through AcceptTheFixture.
public class LineTwinTests
{
    private const string Pinned = "PLang.Tests/Wire/App/Decider/line_golden.json";

    private static List<System.Text.Json.JsonElement> Cases()
        => System.Text.Json.JsonDocument.Parse(System.IO.File.ReadAllText(System.IO.Path.Combine(Fixture.Root(), Pinned)))
            .RootElement.EnumerateArray().ToList();

    // The line one case's moves write.
    private static string? Written(System.Text.Json.JsonElement c)
    {
        var line = new global::app.goal.step.pick.line.@this(c.GetProperty("nests").GetBoolean());
        foreach (var move in c.GetProperty("moves").EnumerateArray())
        {
            var m = move.EnumerateArray().ToList();
            switch (m[0].GetString())
            {
                case "add": line.Add(m[1].GetString()!, m[2].GetBoolean(), m.Count > 3 && m[3].GetBoolean()); break;
                case "insert": line.Insert(m[1].GetString()!); break;
                case "lead": line.Lead(m[1].GetString()!); break;
                case "keep": line.Keep(m[1].GetString()!, m[2].GetString()!); break;
                default: line.Append(m[1].GetString()!); break;
            }
        }
        return line.ToString();
    }

    [Test]
    public async Task EveryCase_WritesThePinnedLine()
    {
        var cases = Cases();
        var differ = new List<string>();
        foreach (var c in cases)
            if (Written(c) is var written && written != c.GetProperty("written").GetString())
                differ.Add($"{c.GetProperty("name").GetString()}\n  pinned: {c.GetProperty("written").GetString()}\n  c#:     {written}");
        await Assert.That(cases.Count).IsEqualTo(11);
        await Assert.That(string.Join("\n", differ)).IsEqualTo("");
    }

    // Re-pins line_golden.json from C#: run by hand after an intended change to the line, then review the diff.
    [Test, Explicit]
    public async Task AcceptTheFixture()
    {
        var pinned = Fixture.Read(Pinned).AsArray();
        var cases = Cases();
        for (int i = 0; i < cases.Count; i++) pinned[i]!["written"] = Written(cases[i]);
        Fixture.Write(Pinned, pinned);
        await Task.CompletedTask;
    }
}
