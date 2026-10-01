namespace PLang.Tests.App.Decider;

// step.Pick and the builder's decider templates against a pinned fixture (pick_golden.json): for each golden goal,
// the decider's recorded answers go through each step's pick.list — the stage-1 request, the stage-2 questions and
// state stage 1's answer gives, prompt C's user message after both answers, and the picks and popular top three
// both answers give. The recorded answers are frozen input; the rest is C#'s own output as pinned. An intended change
// re-pins it through AcceptTheFixture.
public class PickListTests
{
    private const string Pinned = "PLang.Tests/Wire/App/Decider/pick_golden.json";
    private const string SettingsPinned = "PLang.Tests/Wire/App/Decider/settings_golden.json";

    private static System.Text.Json.JsonElement[] Golden()
        => System.Text.Json.JsonDocument.Parse(System.IO.File.ReadAllText(System.IO.Path.Combine(Fixture.Root(), Pinned)))
            .RootElement.EnumerateArray().ToArray();

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
    private static global::app.goal.@this Goal(System.Text.Json.JsonElement entry, global::app.actor.context.@this context)
    {
        var steps = entry.GetProperty("step").EnumerateArray().ToList();
        var goal = Make.Goal(context, entry.GetProperty("name").GetString()!, "/" + entry.GetProperty("name").GetString() + ".goal",
            steps.Select(s => s.GetProperty("kept").GetBoolean()
                ? Make.Step(s.GetProperty("text").GetString()!, s.GetProperty("indent").GetInt32(), Make.Action(context, "file", "read", ("Path", "saved.json")))
                : Make.Step(s.GetProperty("text").GetString()!, s.GetProperty("indent").GetInt32())).ToArray());
        foreach (var s in steps.Where(s => s.GetProperty("kept").GetBoolean()))
            goal.Step[s.GetProperty("index").GetInt32()].PriorText = s.GetProperty("text").GetString();
        return goal;
    }

    private static string DeciderJson => System.IO.Path.Combine(Fixture.Root(), "os", "system", "builder", "llm", "decider.json");

    // The popular actions an unsure step is offered — the builder's decider.json.
    private static List<string> Popular() =>
        System.Text.Json.JsonDocument.Parse(System.IO.File.ReadAllText(DeciderJson))
            .RootElement.GetProperty("popular").EnumerateArray().Select(p => p.GetString()!).ToList();

    // A decider template rendered for the goal, with the variables Decide sets: goal, modules, decider
    // (decider.json) and stage.
    private static async Task<string> Rendered(string template, global::app.goal.@this goal, global::app.actor.context.@this context, int stage = 0)
    {
        context.Variable.Set(new global::app.data.@this("goal", goal, context: context));
        context.Variable.Set(new global::app.data.@this("modules", context.App.module.list, context: context));
        var decider = System.Text.Json.JsonDocument.Parse(System.IO.File.ReadAllText(DeciderJson)).RootElement;
        context.Variable.Set(new global::app.data.@this("decider", Answer(decider, context), context: context));
        context.Variable.Set(new global::app.data.@this("stage", stage, context: context));
        var render = new global::app.module.ui.Render(context)
        {
            Template = (global::app.type.item.text.@this)System.IO.File.ReadAllText(
                System.IO.Path.Combine(Fixture.Root(), "os", "system", "builder", "llm", "templates", template)),
            IsFile = (global::app.type.item.@bool.@this)false,
        };
        var result = await new global::app.module.ui.code.Fluid().Render(render);
        return result.Success ? (await result.Value())!.ToString() : $"render failed: {result.Error!.Message}";
    }

    // ---- what C# makes of one golden goal: each is what the fixture pins

    private static async Task<(string Question, string State)> StageOne(System.Text.Json.JsonElement entry, global::app.actor.context.@this context)
    {
        var goal = Goal(entry, context);
        return (await Rendered("decider1.template", goal, context), await Rendered("decider.state.template", goal, context, stage: 1));
    }

    private static async Task<(string Question, string State)> StageTwo(System.Text.Json.JsonElement entry, global::app.actor.context.@this context)
    {
        var goal = Goal(entry, context);
        var first = Answer(entry.GetProperty("answer1"), context);
        foreach (var step in goal.Step.Items()) await step.Pick.Take(first, Popular(), context);
        return (await Rendered("decider2.template", goal, context), await Rendered("decider.state.template", goal, context, stage: 2));
    }

    // Prompt C's user message after both answers: each step's line (its listing and starting formal), the Types and
    // each listed action once.
    private static async Task<string> UserMessage(System.Text.Json.JsonElement entry, global::app.actor.context.@this context)
    {
        var goal = Goal(entry, context);
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
        return await Rendered("properties.template", goal, context);
    }

    // Each step's picks after both answers, with its popular top three: {"<i>": {"<action>": score, "@popular": {...}}}.
    private static async Task<System.Text.Json.Nodes.JsonObject> Picks(System.Text.Json.JsonElement entry, global::app.actor.context.@this context)
    {
        var goal = Goal(entry, context);
        var first = Answer(entry.GetProperty("answer1"), context);
        var second = Answer(entry.GetProperty("answer2"), context);
        var picks = new System.Text.Json.Nodes.JsonObject();
        foreach (var step in goal.Step.Items())
        {
            await step.Pick.Take(first, Popular(), context);
            await step.Pick.Take(second, Popular(), context);
            var one = new System.Text.Json.Nodes.JsonObject();
            foreach (var p in step.Pick.Item) one[p.Name] = p.Score is { } s ? System.Text.Json.Nodes.JsonValue.Create((double)s) : null;
            if (step.Pick.Top.Count > 0)
            {
                var popular = new System.Text.Json.Nodes.JsonObject();
                foreach (var t in step.Pick.Top) popular[t.Name] = t.Score is { } s ? System.Text.Json.Nodes.JsonValue.Create((double)s) : null;
                one["@popular"] = popular;
            }
            picks[step.Index.ToString()] = one;
        }
        return picks;
    }

    // Prompt C's Settings and Keys blocks for one settings case: the classes its steps name and the %!app.X["key"]%
    // line when a step reads one so.
    private static async Task<string> SettingsBlock(System.Text.Json.JsonElement entry, global::app.actor.context.@this context)
    {
        var name = entry.GetProperty("goal").GetString()!;
        var goal = Make.Goal(context, name, "/" + name + ".goal",
            entry.GetProperty("steps").EnumerateArray().Select(t => Make.Step(t.GetString()!, 0)).ToArray());
        await goal.Step.Scope(context);
        // the message ends in one newline; the block is what comes before it
        var rendered = (await Rendered("properties.template", goal, context)).TrimEnd('\n');
        // the blocks run from the first of their headers to the actions' definitions (or the end)
        var at = new[] { rendered.IndexOf("\n\nSettings\n", StringComparison.Ordinal), rendered.IndexOf("\n\nKeys\n", StringComparison.Ordinal) }
            .Where(i => i >= 0).DefaultIfEmpty(-1).Min();
        var end = at < 0 ? -1 : rendered.IndexOf("\n\n## ", at, StringComparison.Ordinal);
        return at < 0 ? "" : end < 0 ? rendered[at..] : rendered[at..end];
    }

    private static global::app.@this Os() => new global::app.@this(System.IO.Path.Combine(Fixture.Root(), "os")).Testing();

    // ---- the comparisons

    // Two JSON question sets, key by key in order: the differences, or none.
    private static IEnumerable<string> Differ(string at, string rendered, System.Text.Json.JsonElement pinned)
    {
        System.Text.Json.Nodes.JsonObject ours;
        try { ours = System.Text.Json.Nodes.JsonNode.Parse(rendered)!.AsObject(); }
        catch (System.Text.Json.JsonException ex) { return [$"{at}: not JSON ({ex.Message})\n{rendered}"]; }
        var differ = new List<string>();
        var theirs = pinned.EnumerateObject().ToList();
        if (!ours.Select(q => q.Key).SequenceEqual(theirs.Select(q => q.Name)))
            differ.Add($"{at} asks [{string.Join(", ", ours.Select(q => q.Key))}], pinned [{string.Join(", ", theirs.Select(q => q.Name))}]");
        foreach (var q in theirs)
            if (ours[q.Name] is { } mine && !System.Text.Json.Nodes.JsonNode.DeepEquals(mine, System.Text.Json.Nodes.JsonNode.Parse(q.Value.GetRawText())))
                differ.Add($"{at} {q.Name}\n  ours:   {mine.ToJsonString()}\n  pinned: {q.Value.GetRawText()}");
        return differ;
    }

    // Two texts, line by line: the first line that differs, or none.
    private static IEnumerable<string> Differ(string at, string ours, string pinned)
    {
        if (ours == pinned) return [];
        var a = ours.Split('\n'); var b = pinned.Split('\n');
        var n = Enumerable.Range(0, Math.Min(a.Length, b.Length)).FirstOrDefault(i => a[i] != b[i], Math.Min(a.Length, b.Length));
        return [$"{at} differs at line {n + 1} (ours {a.Length} lines, pinned {b.Length})\n  ours:   {(n < a.Length ? a[n] : "<end>")}\n  pinned: {(n < b.Length ? b[n] : "<end>")}"];
    }

    [Test]
    public async Task TheStageOneRequest_IsThePinnedOne()
    {
        await using var os = Os();
        var context = os.actor.list.User.Context;
        var differ = new List<string>();
        foreach (var entry in Golden())
        {
            var at = entry.GetProperty("goal").GetString()!;
            var (question, state) = await StageOne(entry, context);
            differ.AddRange(Differ(at, question, entry.GetProperty("question1")));
            differ.AddRange(Differ(at + " state1", state, entry.GetProperty("state1").GetString()!));
        }
        await Assert.That(string.Join("\n", differ)).IsEqualTo("");
    }

    [Test]
    public async Task ThePromptCUserMessage_IsThePinnedOne()
    {
        await using var os = Os();
        var context = os.actor.list.User.Context;
        var differ = new List<string>();
        foreach (var entry in Golden())
            differ.AddRange(Differ(entry.GetProperty("goal").GetString()!, await UserMessage(entry, context), entry.GetProperty("user").GetString()!));
        await Assert.That(string.Join("\n", differ)).IsEqualTo("");
    }

    [Test]
    public async Task ThePromptCSettingsAndKeys_AreThePinnedOnes()
    {
        await using var os = Os();
        var context = os.actor.list.User.Context;
        var cases = System.Text.Json.JsonDocument.Parse(System.IO.File.ReadAllText(System.IO.Path.Combine(Fixture.Root(), SettingsPinned)))
            .RootElement.EnumerateArray().ToList();
        await Assert.That(cases.Count).IsGreaterThan(0);
        var differ = new List<string>();
        foreach (var entry in cases)
            differ.AddRange(Differ(entry.GetProperty("goal").GetString()!, await SettingsBlock(entry, context), entry.GetProperty("block").GetString()!));
        await Assert.That(string.Join("\n", differ)).IsEqualTo("");
    }

    [Test]
    public async Task TheStageTwoState_IsThePinnedOne()
    {
        await using var os = Os();
        var context = os.actor.list.User.Context;
        var differ = new List<string>();
        foreach (var entry in Golden())
            differ.AddRange(Differ(entry.GetProperty("goal").GetString()! + " state2", (await StageTwo(entry, context)).State, entry.GetProperty("state2").GetString()!));
        await Assert.That(string.Join("\n", differ)).IsEqualTo("");
    }

    // The words are the template's: the app is rooted at the repo's os/ folder, where each action's
    // teaching file (/system/modules/<module>/<action>.description.md) is.
    [Test]
    public async Task StageOnesAnswer_RendersThePinnedStageTwoQuestions()
    {
        await using var os = Os();
        var context = os.actor.list.User.Context;
        var differ = new List<string>();
        foreach (var entry in Golden())
            differ.AddRange(Differ(entry.GetProperty("goal").GetString()!, (await StageTwo(entry, context)).Question, entry.GetProperty("question2")));
        await Assert.That(string.Join("\n", differ)).IsEqualTo("");
    }

    [Test]
    public async Task BothAnswers_GiveThePinnedPicks()
    {
        await using var app = new global::app.@this("/app").Testing();
        var context = app.actor.list.User.Context;
        var differ = new List<string>();
        foreach (var entry in Golden())
        {
            var ours = await Picks(entry, context);
            var pinned = System.Text.Json.Nodes.JsonNode.Parse(entry.GetProperty("picks").GetRawText())!;
            foreach (var (index, step) in ours)
                if (!System.Text.Json.Nodes.JsonNode.DeepEquals(step, pinned[index]))
                    differ.Add($"{entry.GetProperty("goal").GetString()}[{index}]\n  ours:   {step!.ToJsonString()}\n  pinned: {pinned[index]?.ToJsonString()}");
        }
        await Assert.That(string.Join("\n", differ)).IsEqualTo("");
    }

    // A guide (module.guide.md, <action>.guide.md) is the learner's, never the builder's: with a guide beside every module
    // and beside every action, no request the builder sends for the golden goals holds either (stage 1's questions and
    // state, stage 2's questions and state, prompt C's user message). The app is rooted at a copy of the modules' docs,
    // file's and read's descriptions marked, so the requests are seen to read the copy.
    [Test]
    public async Task AGuide_ReachesNoRequestTheBuilderSends()
    {
        var root = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "plang-guide-" + System.Guid.NewGuid().ToString("N")[..8]);
        var source = System.IO.Path.Combine(Fixture.Root(), "os", "system", "modules");
        var modules = System.IO.Path.Combine(root, "system", "modules");
        try
        {
            foreach (var file in System.IO.Directory.EnumerateFiles(source, "*.md", System.IO.SearchOption.AllDirectories))
            {
                var copy = System.IO.Path.Combine(modules, System.IO.Path.GetRelativePath(source, file));
                System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(copy)!);
                System.IO.File.Copy(file, copy);
            }
            foreach (var folder in System.IO.Directory.GetDirectories(modules))
                System.IO.File.WriteAllText(System.IO.Path.Combine(folder, "module.guide.md"), "MODULE-GUIDE-ONLY prose for a learner.");
            System.IO.File.AppendAllText(System.IO.Path.Combine(modules, "file", "module.description.md"), " DESCRIPTION-MARK");
            System.IO.File.AppendAllText(System.IO.Path.Combine(modules, "file", "read.description.md"), " ACTION-DESCRIPTION-MARK");

            await using var os = new global::app.@this(root).Testing();
            var context = os.actor.list.User.Context;
            foreach (var module in os.module.list.Items())
                foreach (var action in module.ActionNames)
                {
                    var folder = System.IO.Path.Combine(modules, module.Name);
                    System.IO.Directory.CreateDirectory(folder);
                    System.IO.File.WriteAllText(System.IO.Path.Combine(folder, action + ".guide.md"), "ACTION-GUIDE-ONLY prose for a learner.");
                }
            var requests = new List<string>();
            foreach (var entry in Golden())
            {
                var (question1, state1) = await StageOne(entry, context);
                var (question2, state2) = await StageTwo(entry, context);
                requests.AddRange([question1, state1, question2, state2, await UserMessage(entry, context)]);
            }

            await Assert.That(requests.Where(r => r.StartsWith("render failed")).ToList()).IsEmpty();
            await Assert.That(requests.Any(r => r.Contains("DESCRIPTION-MARK"))).IsTrue();
            await Assert.That(requests.Any(r => r.Contains("ACTION-DESCRIPTION-MARK"))).IsTrue();
            await Assert.That(requests.Count(r => r.Contains("MODULE-GUIDE-ONLY"))).IsEqualTo(0);
            await Assert.That(requests.Count(r => r.Contains("ACTION-GUIDE-ONLY"))).IsEqualTo(0);
        }
        finally
        {
            if (System.IO.Directory.Exists(root)) System.IO.Directory.Delete(root, true);
        }
    }

    // Re-pins pick_golden.json and settings_golden.json from C#: run by hand after an intended change to the decider's
    // templates, prompt C or the pick, then review the fixtures' diff and commit it.
    [Test, Explicit]
    public async Task AcceptTheFixture()
    {
        await using var os = Os();
        var context = os.actor.list.User.Context;
        var golden = Fixture.Read(Pinned).AsArray();
        var entries = Golden();
        for (int i = 0; i < entries.Length; i++)
        {
            var (question1, state1) = await StageOne(entries[i], context);
            var (question2, state2) = await StageTwo(entries[i], context);
            golden[i]!["question1"] = System.Text.Json.Nodes.JsonNode.Parse(question1);
            golden[i]!["state1"] = state1;
            golden[i]!["question2"] = System.Text.Json.Nodes.JsonNode.Parse(question2);
            golden[i]!["state2"] = state2;
            golden[i]!["user"] = await UserMessage(entries[i], context);
            golden[i]!["picks"] = await Picks(entries[i], context);
        }
        Fixture.Write(Pinned, golden);

        var settings = Fixture.Read(SettingsPinned).AsArray();
        var cases = System.Text.Json.JsonDocument.Parse(System.IO.File.ReadAllText(System.IO.Path.Combine(Fixture.Root(), SettingsPinned)))
            .RootElement.EnumerateArray().ToArray();
        for (int i = 0; i < cases.Length; i++) settings[i]!["block"] = await SettingsBlock(cases[i], context);
        Fixture.Write(SettingsPinned, settings);
    }
}
