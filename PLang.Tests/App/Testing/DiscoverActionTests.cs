using System.Text.Json;
using global::app.Tester;
using global::app.Utils;

namespace PLang.Tests.App.Tester;

/// <summary>
/// Batch 8 — test.discover action.
/// C# handler: filesystem walk + .pr parsing. Inputs: Path (default "."), Pattern
/// (default "*.test.goal"). Returns List&lt;global::app.Tester.File&gt; with file path, entry goal,
/// .pr path, tags, status.
///
/// Freshness uses Goal.Hash (SHA-256 over Name + Steps.Text, [Store]-persisted in
/// .pr). Comment-only edits to a .goal DO NOT trigger stale — only Name/Step.Text
/// changes do. This is intentional: comments and whitespace shouldn't force rebuilds.
///
/// Auto-tags come from [RequiresCapability] on action handlers. Tags propagate across
/// sub-goals reached via static goal.call chains; dynamic goal names (via %var%) are
/// not traversed.
/// </summary>
public class DiscoverActionTests
{
    private string _tempDir = null!;
    private global::app.@this _app = null!;

    [Before(Test)]
    public void Setup()
    {
        _tempDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            "plang-discover-" + Guid.NewGuid().ToString("N")[..8]);
        System.IO.Directory.CreateDirectory(_tempDir);
        var fs = new global::app.FileSystem.Default.PLangFileSystem(_tempDir, "");
        _app = new global::app.@this(fs);
    }

    [After(Test)]
    public async Task Teardown()
    {
        await _app.DisposeAsync();
        if (System.IO.Directory.Exists(_tempDir))
            System.IO.Directory.Delete(_tempDir, true);
    }

    // Variant that lets the caller pass fully-constructed Data parameters (with Type hints).
    private string CreateTestFileWithAction(string relativePath, string goalName, string[] stepTexts,
        (string module, string actionName, List<Data> parameters)[] actions,
        string? prBuilderVersion = null)
    {
        var normalized = actions.Select(a => (a.module, a.actionName,
            a.parameters.Select(p => (p.Name, (object?)p.Value)).ToArray())).ToArray();
        return CreateTestFile(relativePath, goalName, stepTexts, normalized, prBuilderVersion,
            preConstructedParams: actions);
    }

    // Creates a .test.goal file and a matching .pr that share the same Goal.Hash.
    // Returns the relative path (forward slashes).
    private string CreateTestFile(string relativePath, string goalName, string[] stepTexts,
        (string module, string actionName, (string name, object? value)[] parameters)[]? actions = null,
        string? prBuilderVersion = null,
        bool corruptHash = false,
        bool prMissing = false,
        (string module, string actionName, List<Data> parameters)[]? preConstructedParams = null)
    {
        actions ??= stepTexts.Select(_ => ("variable", "set",
            new (string, object?)[] { ("Name", "x"), ("Value", 1) })).ToArray();

        var absFile = System.IO.Path.Combine(_tempDir, relativePath.Replace('/', System.IO.Path.DirectorySeparatorChar));
        var absDir = System.IO.Path.GetDirectoryName(absFile)!;
        System.IO.Directory.CreateDirectory(absDir);

        // Write .goal text matching Goal.Parse's format.
        var goalText = new System.Text.StringBuilder();
        goalText.AppendLine(goalName);
        foreach (var text in stepTexts)
            goalText.AppendLine("- " + text);
        System.IO.File.WriteAllText(absFile, goalText.ToString());

        if (prMissing) return relativePath;

        // Build a Goal with matching steps+actions, serialize to .pr JSON.
        var goal = new Goal
        {
            Name = goalName,
            Path = "/" + relativePath,
            BuilderVersion = prBuilderVersion,
            Steps = new GoalSteps()
        };
        for (int i = 0; i < stepTexts.Length; i++)
        {
            var step = new Step { Index = i, Text = stepTexts[i] };
            var actionSpec = actions[i];
            List<Data> parameters;
            if (preConstructedParams != null && i < preConstructedParams.Length)
            {
                parameters = preConstructedParams[i].parameters;
            }
            else
            {
                parameters = actionSpec.parameters
                    .Select(p => new Data(p.name, p.value))
                    .ToList();
            }
            step.Actions.Add(new PrAction
            {
                Module = actionSpec.module,
                ActionName = actionSpec.actionName,
                Parameters = parameters
            });
            goal.Steps.Add(step);
        }
        // Snapshot the canonical hash. If corruptHash, mutate one step text AFTER
        // the hash is locked in, so the stored .pr's hash diverges from a fresh parse.
        var _ = goal.Hash;

        var prDir = System.IO.Path.Combine(absDir, ".build");
        System.IO.Directory.CreateDirectory(prDir);
        var prFile = System.IO.Path.Combine(prDir,
            System.IO.Path.GetFileNameWithoutExtension(absFile).ToLowerInvariant() + ".pr");

        if (corruptHash)
        {
            // Store the .pr with a deliberately bogus hash so freshness check flips stale.
            var json = JsonSerializer.Serialize(goal, global::app.Utils.Json.CamelCaseIndented);
            var doc = JsonSerializer.Deserialize<Dictionary<string, object?>>(json);
            doc!["hash"] = "0000000000000000000000000000000000000000000000000000000000000000";
            json = JsonSerializer.Serialize(doc, global::app.Utils.Json.CamelCaseIndented);
            System.IO.File.WriteAllText(prFile, json);
        }
        else
        {
            System.IO.File.WriteAllText(prFile,
                JsonSerializer.Serialize(goal, global::app.Utils.Json.CamelCaseIndented));
        }

        return relativePath;
    }

    private async Task<List<global::app.Tester.File>> Discover(string path = ".", bool recursive = true)
    {
        var action = new global::app.modules.test.discover
        {
            Context = _app.User.Context,
            Path = new global::app.data.@this<string>("Path", path),
            Pattern = new global::app.data.@this<string>("Pattern", "*.test.goal"),
            Recursive = new global::app.data.@this<bool>("Recursive", recursive)
        };
        var result = await action.Run();
        return result.Value as List<global::app.Tester.File> ?? new List<global::app.Tester.File>();
    }

    // Walks the tree of *.test.goal files under the target path; every match surfaces
    // in the returned List<global::app.Tester.File>.
    [Test]
    public async Task Discover_RecursiveWalk_FindsAllTestGoalFiles()
    {
        CreateTestFile("Foo.test.goal", "Start", new[] { "set %x% = 1" });
        CreateTestFile("sub/Bar.test.goal", "Start", new[] { "set %x% = 2" });
        CreateTestFile("sub/deep/Baz.test.goal", "Start", new[] { "set %x% = 3" });

        var files = await Discover();

        await Assert.That(files.Count).IsEqualTo(3);
        await Assert.That(files.Any(f => f.Path.EndsWith("Foo.test.goal"))).IsTrue();
        await Assert.That(files.Any(f => f.Path.EndsWith("Bar.test.goal"))).IsTrue();
        await Assert.That(files.Any(f => f.Path.EndsWith("Baz.test.goal"))).IsTrue();
    }

    // A .goal with no matching .pr in .build/ → global::app.Tester.Status.Stale with reason "no .pr".
    [Test]
    public async Task Discover_NoPrFile_MarksStaleWithReasonNoPr()
    {
        CreateTestFile("Foo.test.goal", "Start", new[] { "set %x% = 1" }, prMissing: true);

        var files = await Discover();
        var file = files.Single();

        await Assert.That(file.Status).IsEqualTo(global::app.Tester.Status.Stale);
        await Assert.That(file.StatusReason).IsEqualTo("no .pr");
    }

    // Fresh Goal.Hash (from current .goal) differs from the hash stored in the .pr
    // (Name or Step.Text changed since last build) → global::app.Tester.Status.Stale with reason
    // "rebuild needed". Comment-only edits do NOT trigger stale.
    [Test]
    public async Task Discover_GoalAndPrHashMismatch_MarksStaleRebuildNeeded()
    {
        CreateTestFile("Foo.test.goal", "Start", new[] { "set %x% = 1" }, corruptHash: true);

        var files = await Discover();
        var file = files.Single();

        await Assert.That(file.Status).IsEqualTo(global::app.Tester.Status.Stale);
        await Assert.That(file.StatusReason).IsEqualTo("rebuild needed");
    }

    // Scans .pr for test.tag actions, collects their Tags parameter into the test's
    // user-tag set. Multiple test.tag actions accumulate.
    [Test]
    public async Task Discover_UserTags_ExtractedFromTestTagActionInPr()
    {
        CreateTestFile("Foo.test.goal", "Start",
            new[] { "set test tag 'http', 'fast'", "set test tag 'slow'", "set %x% = 1" },
            new (string, string, (string, object?)[])[]
            {
                ("test", "tag", new (string, object?)[] { ("Tags", new List<object?> { "http", "fast" }) }),
                ("test", "tag", new (string, object?)[] { ("Tags", new List<object?> { "slow" }) }),
                ("variable", "set", new (string, object?)[] { ("Name", "x"), ("Value", 1) })
            });

        var files = await Discover();
        var file = files.Single();

        await Assert.That(file.Tags.Contains("http")).IsTrue();
        await Assert.That(file.Tags.Contains("fast")).IsTrue();
        await Assert.That(file.Tags.Contains("slow")).IsTrue();
    }

    // For each action in the .pr, resolves the handler class (via App.Modules.
    // GetCodeGenerated) and reads [RequiresCapability] via reflection. Capabilities
    // union into the test's auto-tag set. e.g. a test using http.request gains "network".
    [Test]
    public async Task Discover_AutoTags_ExtractedFromHandlerAttributes()
    {
        CreateTestFile("Foo.test.goal", "Start",
            new[] { "http get https://example.com" },
            new (string, string, (string, object?)[])[]
            {
                ("http", "request", new (string, object?)[] { ("Url", "https://example.com") })
            });

        var files = await Discover();
        var file = files.Single();

        await Assert.That(file.Tags.Contains("network")).IsTrue();
    }

    // Sub-goal reached via static goal.call: its actions' capabilities propagate up
    // to the caller test. Test uses goal.call "Helper" where Helper uses http.request
    // → test gains "network" auto-tag even though its own entry goal doesn't use http.
    [Test]
    public async Task Discover_AutoTags_TraverseSubGoals_UnionsAcrossCallChain()
    {
        // Register a Helper goal in the App (not a separate .test.goal) that uses http.
        var helper = new Goal
        {
            Name = "Helper",
            Path = "/Helper.goal",
            Steps = new GoalSteps
            {
                new Step
                {
                    Index = 0, Text = "http get",
                    Actions = new StepActions
                    {
                        new PrAction
                        {
                            Module = "http", ActionName = "request",
                            Parameters = new List<Data> { new("Url", "https://example.com") }
                        }
                    }
                }
            }
        };
        _app.Goals.Add(helper);

        CreateTestFileWithAction("Foo.test.goal", "Start",
            new[] { "call Helper" },
            new (string module, string actionName, List<Data> parameters)[]
            {
                ("goal", "call", new List<Data>
                {
                    new("GoalName", new GoalCall { Name = "Helper" }, global::app.data.type.FromName("goal.call"))
                })
            });

        var files = await Discover();
        var file = files.Single();

        await Assert.That(file.Tags.Contains("network")).IsTrue();
    }

    // Config.Include=["fast"]: tests without the "fast" tag are returned as
    // global::app.Tester.Status.Skipped — not removed from the list, so the run reports them as
    // skipped (CI visibility).
    [Test]
    public async Task Discover_IncludeFilter_NonMatchingTests_MarkedSkipped()
    {
        _app.Tester.Include.Add("fast");
        CreateTestFile("Foo.test.goal", "Start", new[] { "set %x% = 1" });  // no tags

        var files = await Discover();
        var file = files.Single();

        await Assert.That(file.Status).IsEqualTo(global::app.Tester.Status.Skipped);
    }

    // Config.Exclude=["slow"]: tests carrying the "slow" tag are returned as
    // global::app.Tester.Status.Skipped.
    [Test]
    public async Task Discover_ExcludeFilter_MatchingTests_MarkedSkipped()
    {
        _app.Tester.Exclude.Add("slow");
        CreateTestFile("Foo.test.goal", "Start",
            new[] { "set test tag 'slow'", "set %x% = 1" },
            new (string, string, (string, object?)[])[]
            {
                ("test", "tag", new (string, object?)[] { ("Tags", new List<object?> { "slow" }) }),
                ("variable", "set", new (string, object?)[] { ("Name", "x"), ("Value", 1) })
            });

        var files = await Discover();
        var file = files.Single();

        await Assert.That(file.Status).IsEqualTo(global::app.Tester.Status.Skipped);
    }

    // Filter composition: Include=["http"], Exclude=["slow"]. A test tagged
    // [http, slow] ends up Skipped — exclude wins on conflict.
    // (independent — boundary, locks the filter-order semantics)
    [Test]
    public async Task Discover_IncludeAndExclude_ExcludeAppliedAfterInclude()
    {
        _app.Tester.Include.Add("http");
        _app.Tester.Exclude.Add("slow");
        CreateTestFile("Foo.test.goal", "Start",
            new[] { "set test tag 'http', 'slow'", "set %x% = 1" },
            new (string, string, (string, object?)[])[]
            {
                ("test", "tag", new (string, object?)[] { ("Tags", new List<object?> { "http", "slow" }) }),
                ("variable", "set", new (string, object?)[] { ("Name", "x"), ("Value", 1) })
            });

        var files = await Discover();
        var file = files.Single();

        await Assert.That(file.Status).IsEqualTo(global::app.Tester.Status.Skipped);
    }

    // Robustness: a path that doesn't exist does not throw; returns empty list.
    // Logged for diagnostics but not fatal — discovery is non-destructive.
    // (independent — robustness)
    [Test]
    public async Task Discover_NonExistentPath_ReturnsEmptyList_NoError()
    {
        var files = await Discover("does-not-exist");
        await Assert.That(files.Count).IsEqualTo(0);
    }
}
