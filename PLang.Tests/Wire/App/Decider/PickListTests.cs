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

    // The golden goal; a kept step (already built, its text unchanged) holds its saved code and its
    // prior text, as goal.Merge leaves it.
    private static global::app.goal.@this Goal(System.Text.Json.JsonElement entry)
    {
        var steps = entry.GetProperty("step").EnumerateArray().ToList();
        var goal = Make.Goal(entry.GetProperty("name").GetString()!, "/" + entry.GetProperty("name").GetString() + ".goal",
            steps.Select(s => s.GetProperty("kept").GetBoolean()
                ? Make.Step(s.GetProperty("text").GetString()!, s.GetProperty("indent").GetInt32(), Make.Action("file", "read", ("Path", "saved.json")))
                : Make.Step(s.GetProperty("text").GetString()!, s.GetProperty("indent").GetInt32())).ToArray());
        foreach (var s in steps.Where(s => s.GetProperty("kept").GetBoolean()))
            goal.Step[s.GetProperty("index").GetInt32()].PriorText = s.GetProperty("text").GetString();
        return goal;
    }

    private static string RepoRoot()
    {
        var dir = System.AppContext.BaseDirectory;
        while (dir != null && !System.IO.Directory.Exists(System.IO.Path.Combine(dir, "PLang", "app")))
            dir = System.IO.Directory.GetParent(dir)?.FullName;
        return dir!;
    }

    // The popular actions an unsure step is offered — the builder's decider.json, which python reads too.
    private static List<string> Popular() =>
        System.Text.Json.JsonDocument.Parse(System.IO.File.ReadAllText(System.IO.Path.Combine(RepoRoot(), "os", "system", "builder", "llm", "decider.json")))
            .RootElement.GetProperty("popular").EnumerateArray().Select(p => p.GetString()!).ToList();

    // A decider template rendered for the goal, with the variables Decide sets: goal, modules, decider
    // (decider.json) and stage.
    private static async Task<string> Rendered(string template, global::app.goal.@this goal, global::app.actor.context.@this context, int stage = 0)
    {
        context.Variable.Set(new global::app.data.@this("goal", goal, context: context));
        context.Variable.Set(new global::app.data.@this("modules", context.App.Module.list, context: context));
        var decider = System.Text.Json.JsonDocument.Parse(System.IO.File.ReadAllText(
            System.IO.Path.Combine(RepoRoot(), "os", "system", "builder", "llm", "decider.json"))).RootElement;
        context.Variable.Set(new global::app.data.@this("decider", Answer(decider, context), context: context));
        context.Variable.Set(new global::app.data.@this("stage", stage, context: context));
        var render = new global::app.module.action.ui.Render(context)
        {
            Template = (global::app.type.item.text.@this)System.IO.File.ReadAllText(
                System.IO.Path.Combine(RepoRoot(), "os", "system", "builder", "llm", "templates", template)),
            IsFile = (global::app.type.item.@bool.@this)false,
        };
        var result = await new global::app.module.action.ui.code.Fluid().Render(render);
        return result.Success ? (await result.Value())!.ToString() : $"render failed: {result.Error!.Message}";
    }

    // Two JSON question sets, key by key in order: the differences, or none.
    private static IEnumerable<string> Differ(string at, string rendered, System.Text.Json.JsonElement python)
    {
        System.Text.Json.Nodes.JsonObject ours;
        try { ours = System.Text.Json.Nodes.JsonNode.Parse(rendered)!.AsObject(); }
        catch (System.Text.Json.JsonException ex) { return [$"{at}: not JSON ({ex.Message})\n{rendered}"]; }
        var differ = new List<string>();
        var theirs = python.EnumerateObject().ToList();
        if (!ours.Select(q => q.Key).SequenceEqual(theirs.Select(q => q.Name)))
            differ.Add($"{at} asks [{string.Join(", ", ours.Select(q => q.Key))}], python [{string.Join(", ", theirs.Select(q => q.Name))}]");
        foreach (var q in theirs)
            if (ours[q.Name] is { } mine && !System.Text.Json.Nodes.JsonNode.DeepEquals(mine, System.Text.Json.Nodes.JsonNode.Parse(q.Value.GetRawText())))
                differ.Add($"{at} {q.Name}\n  ours:   {mine.ToJsonString()}\n  python: {q.Value.GetRawText()}");
        return differ;
    }

    // Two state texts, line by line: the first line that differs, or none.
    private static IEnumerable<string> Differ(string at, string ours, string python)
    {
        if (ours == python) return [];
        var a = ours.Split('\n'); var b = python.Split('\n');
        var n = Enumerable.Range(0, Math.Min(a.Length, b.Length)).FirstOrDefault(i => a[i] != b[i], Math.Min(a.Length, b.Length));
        return [$"{at} state differs at line {n + 1} (ours {a.Length} lines, python {b.Length})\n  ours:   {(n < a.Length ? a[n] : "<end>")}\n  python: {(n < b.Length ? b[n] : "<end>")}"];
    }

    [Test]
    public async Task TheStageOneRequest_IsTheOnePythonSends()
    {
        await using var os = TestApp.Create(System.IO.Path.Combine(RepoRoot(), "os"));
        var context = os.User.Context;
        var differ = new List<string>();
        foreach (var entry in Golden())
        {
            var goal = Goal(entry);
            var at = entry.GetProperty("goal").GetString()!;
            differ.AddRange(Differ(at, await Rendered("decider1.template", goal, context), entry.GetProperty("question1")));
            differ.AddRange(Differ(at, await Rendered("decider.state.template", goal, context, stage: 1), entry.GetProperty("state1").GetString()!));
        }
        await Assert.That(string.Join("\n", differ)).IsEqualTo("");
    }

    // Prompt C's user message after both answers: each step's line (its listing and starting formal),
    // the Types and each listed action once — byte for byte what the eval sends.
    [Test]
    public async Task ThePromptCUserMessage_IsTheOnePythonSends()
    {
        await using var os = TestApp.Create(System.IO.Path.Combine(RepoRoot(), "os"));
        var context = os.User.Context;
        var differ = new List<string>();
        foreach (var entry in Golden())
        {
            var goal = Goal(entry);
            foreach (var s in entry.GetProperty("step").EnumerateArray())
                if (s.GetProperty("comment").GetString() is { } comment) goal.Step[s.GetProperty("index").GetInt32()].Comment = comment;
            var first = Answer(entry.GetProperty("answer1"), context);
            var second = Answer(entry.GetProperty("answer2"), context);
            foreach (var step in goal.Step.Items())
            {
                await step.Pick.Take(first, Popular(), context);
                await step.Pick.Take(second, Popular(), context);
            }
            await goal.Step.Scope(context);   // build.pick's walk: each step's => types:
            differ.AddRange(Differ(entry.GetProperty("goal").GetString()!,
                await Rendered("properties.template", goal, context), entry.GetProperty("user").GetString()!));
        }
        await Assert.That(string.Join("\n", differ)).IsEqualTo("");
    }

    [Test]
    public async Task TheStageTwoState_IsTheOnePythonSends()
    {
        await using var os = TestApp.Create(System.IO.Path.Combine(RepoRoot(), "os"));
        var context = os.User.Context;
        var differ = new List<string>();
        foreach (var entry in Golden())
        {
            var goal = Goal(entry);
            var first = Answer(entry.GetProperty("answer1"), context);
            foreach (var step in goal.Step.Items()) await step.Pick.Take(first, Popular(), context);
            differ.AddRange(Differ(entry.GetProperty("goal").GetString()!,
                await Rendered("decider.state.template", goal, context, stage: 2), entry.GetProperty("state2").GetString()!));
        }
        await Assert.That(string.Join("\n", differ)).IsEqualTo("");
    }

    // The words are the template's: the app is rooted at the repo's os/ folder, where each action's
    // teaching file (/system/modules/<module>/<action>.description.md) is.
    [Test]
    public async Task StageOnesAnswer_RendersTheStageTwoQuestionsPythonAsked()
    {
        await using var os = TestApp.Create(System.IO.Path.Combine(RepoRoot(), "os"));
        var context = os.User.Context;
        var differ = new List<string>();
        foreach (var entry in Golden())
        {
            var goal = Goal(entry);
            var first = Answer(entry.GetProperty("answer1"), context);
            foreach (var step in goal.Step.Items()) await step.Pick.Take(first, Popular(), context);
            differ.AddRange(Differ(entry.GetProperty("goal").GetString()!,
                await Rendered("decider2.template", goal, context), entry.GetProperty("question2")));
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
                await step.Pick.Take(first, Popular(), context);
                await step.Pick.Take(second, Popular(), context);
                var at = $"{entry.GetProperty("goal").GetString()}[{step.Index}]";                var python = entry.GetProperty("picks").GetProperty(step.Index.ToString());
                var theirs = python.EnumerateObject().Where(p => p.Name != "@popular")
                    .Select(p => $"{p.Name} {(p.Value.ValueKind == System.Text.Json.JsonValueKind.Null ? "null" : p.Value.GetDouble().ToString("R"))}");
                var ours = step.Pick.Item.Select(p => $"{p.Name} {(p.Score is { } s ? ((double)s).ToString("R") : "null")}");
                if (!ours.SequenceEqual(theirs)) differ.Add($"{at}\n  ours:   {string.Join(", ", ours)}\n  python: {string.Join(", ", theirs)}");
                var popular = python.TryGetProperty("@popular", out var p) ? p.EnumerateObject().Select(o => o.Name) : [];
                if (!step.Pick.Top.Select(t => t.Name).SequenceEqual(popular))
                    differ.Add($"{at} popular: ours [{string.Join(", ", step.Pick.Top.Select(t => t.Name))}], python [{string.Join(", ", popular)}]");
            }
        }
        await Assert.That(string.Join("\n", differ)).IsEqualTo("");
    }
}
