namespace PLang.Tests.App.Decider;

// The pre-filled formal line (goal/step/pick/line) against the eval's twin (tools/decider/prompt_c.Line): the same
// moves write the same line — a lone if holds the step's actions in its { }, a chain stays flat
// (line_golden.json, written by tools/decider/line_fixture.py).
public class LineTwinTests
{
    [Test]
    public async Task EveryCase_WritesTheLinePythonWrites()
    {
        var dir = new System.IO.DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !System.IO.File.Exists(System.IO.Path.Combine(dir.FullName, "PLang.Tests", "Wire", "App", "Decider", "line_golden.json")))
            dir = dir.Parent;
        var cases = System.Text.Json.JsonDocument.Parse(System.IO.File.ReadAllText(
            System.IO.Path.Combine(dir!.FullName, "PLang.Tests", "Wire", "App", "Decider", "line_golden.json"))).RootElement.EnumerateArray().ToList();
        var differ = new List<string>();
        foreach (var c in cases)
        {
            var line = new global::app.goal.step.pick.line.@this(c.GetProperty("nests").GetBoolean());
            foreach (var move in c.GetProperty("moves").EnumerateArray())
            {
                var m = move.EnumerateArray().ToList();
                switch (m[0].GetString())
                {
                    case "add": line.Add(m[1].GetString()!, m[2].GetBoolean()); break;
                    case "insert": line.Insert(m[1].GetString()!); break;
                    default: line.Append(m[1].GetString()!); break;
                }
            }
            var expected = c.GetProperty("written").GetString();
            if (line.ToString() != expected) differ.Add($"{c.GetProperty("name").GetString()}\n  python: {expected}\n  c#:     {line}");
        }
        await Assert.That(cases.Count).IsEqualTo(7);
        await Assert.That(string.Join("\n", differ)).IsEqualTo("");
    }
}
