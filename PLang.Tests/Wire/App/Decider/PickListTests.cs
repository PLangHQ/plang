namespace PLang.Tests.App.Decider;

// step.Pick against the python decider (harness.picks / stage2, decider_eval.one): for each golden goal,
// the decider's recorded answers (pick_golden.json, written by tools/decider/pick_fixture.py from an eval
// round) go through each step's pick.list — stage 1's answer gives the same stage-2 questions python
// asked, and both answers give the same picks and popular top three python read off them.
public class PickListTests
{
    private static System.Text.Json.JsonElement[] Golden()
    {
        var dir = new System.IO.DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !System.IO.File.Exists(System.IO.Path.Combine(dir.FullName, "PLang.Tests", "Wire", "App", "Decider", "pick_golden.json")))
            dir = dir.Parent;
        var path = System.IO.Path.Combine(dir!.FullName, "PLang.Tests", "Wire", "App", "Decider", "pick_golden.json");
        return System.Text.Json.JsonDocument.Parse(System.IO.File.ReadAllText(path)).RootElement.EnumerateArray().ToArray();
    }

    // A JSON value as the raw CLR a decider answer arrives as: objects, lists, numbers, text, bools.
    private static object? Raw(System.Text.Json.JsonElement e) => e.ValueKind switch
    {
        System.Text.Json.JsonValueKind.Object => e.EnumerateObject().ToDictionary(p => p.Name, p => Raw(p.Value)),
        System.Text.Json.JsonValueKind.Array => e.EnumerateArray().Select(Raw).ToList(),
        System.Text.Json.JsonValueKind.Number => e.GetDouble(),
        System.Text.Json.JsonValueKind.String => e.GetString(),
        System.Text.Json.JsonValueKind.True => true,
        System.Text.Json.JsonValueKind.False => false,
        _ => null,
    };

    private static global::app.type.item.dict.@this Answer(System.Text.Json.JsonElement e, global::app.actor.context.@this context)
        => Make.Dict((System.Collections.IDictionary)Raw(e)!, context);

    private static global::app.goal.@this Goal(System.Text.Json.JsonElement entry)
        => Make.Goal(entry.GetProperty("goal").GetString()!, "/" + entry.GetProperty("goal").GetString() + ".goal",
            entry.GetProperty("step").EnumerateArray().Select(s => Make.Step(s.GetProperty("text").GetString()!)).ToArray());

    private static async Task<string> Json(global::app.type.item.dict.@this dict, global::app.actor.context.@this context)
    {
        var json = new global::app.channel.serializer.Json(context);
        using var ms = new System.IO.MemoryStream();
        await json.SerializeAsync(ms, new global::app.data.@this("", dict, context: context));
        return System.Text.Encoding.UTF8.GetString(ms.ToArray());
    }

    private static string RepoRoot()
    {
        var dir = System.AppContext.BaseDirectory;
        while (dir != null && !System.IO.Directory.Exists(System.IO.Path.Combine(dir, "PLang", "app")))
            dir = System.IO.Directory.GetParent(dir)?.FullName;
        return dir!;
    }

    // The questions quote each action's teaching file (/system/modules/<module>/<action>.description.md):
    // the app is rooted at the repo's os/ folder, where they are.
    [Test]
    public async Task StageOnesAnswer_AsksTheStageTwoQuestionsPythonAsked()
    {
        await using var os = TestApp.Create(System.IO.Path.Combine(RepoRoot(), "os"));
        var context = os.User.Context;
        var differ = new List<string>();
        foreach (var entry in Golden())
        {
            var goal = Goal(entry);
            var first = Answer(entry.GetProperty("answer1"), context);
            foreach (var step in goal.Step.Items())
            {
                await step.Pick.Take(first, context);
                var ours = System.Text.Json.Nodes.JsonNode.Parse(await Json(await step.Pick.Question(context), context))!.AsObject();
                var theirs = entry.GetProperty("question2").EnumerateObject().Where(q => q.Name.StartsWith($"s{step.Index}_")).ToList();
                var at = $"{entry.GetProperty("goal").GetString()}[{step.Index}]";
                if (!ours.Select(q => q.Key).SequenceEqual(theirs.Select(q => q.Name)))
                    differ.Add($"{at} asks [{string.Join(", ", ours.Select(q => q.Key))}], python [{string.Join(", ", theirs.Select(q => q.Name))}]");
                foreach (var q in theirs)
                    if (ours[q.Name] is { } mine && !System.Text.Json.Nodes.JsonNode.DeepEquals(mine, System.Text.Json.Nodes.JsonNode.Parse(q.Value.GetRawText())))
                        differ.Add($"{at} {q.Name}\n  ours:   {mine.ToJsonString()}\n  python: {q.Value.GetRawText()}");
            }
        }
        await Assert.That(string.Join("\n", differ)).IsEqualTo("");
    }

    [Test]
    public async Task BothAnswers_GiveThePicksPythonRead()
    {
        var context = TestApp.SharedContext;
        var differ = new List<string>();
        foreach (var entry in Golden())
        {
            var goal = Goal(entry);
            var first = Answer(entry.GetProperty("answer1"), context);
            var second = Answer(entry.GetProperty("answer2"), context);
            foreach (var step in goal.Step.Items())
            {
                await step.Pick.Take(first, context);
                await step.Pick.Take(second, context);
                var at = $"{entry.GetProperty("goal").GetString()}[{step.Index}]";                var python = entry.GetProperty("picks").GetProperty(step.Index.ToString());
                var theirs = python.EnumerateObject().Where(p => p.Name != "@popular")
                    .Select(p => $"{p.Name} {(p.Value.ValueKind == System.Text.Json.JsonValueKind.Null ? "null" : p.Value.GetDouble().ToString("R"))}");
                var ours = step.Pick.Select(p => $"{p.Name} {(p.Score is { } s ? ((double)s).ToString("R") : "null")}");
                if (!ours.SequenceEqual(theirs)) differ.Add($"{at}\n  ours:   {string.Join(", ", ours)}\n  python: {string.Join(", ", theirs)}");
                var popular = python.TryGetProperty("@popular", out var p) ? p.EnumerateObject().Select(o => o.Name) : [];
                if (!step.Pick.Top.Select(t => t.Name).SequenceEqual(popular))
                    differ.Add($"{at} popular: ours [{string.Join(", ", step.Pick.Top.Select(t => t.Name))}], python [{string.Join(", ", popular)}]");
            }
        }
        await Assert.That(string.Join("\n", differ)).IsEqualTo("");
    }
}
