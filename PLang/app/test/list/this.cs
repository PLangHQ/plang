using app.test;

namespace app.test.list;

/// <summary>
/// The run's tests — a list of them, reached at <c>app.test.list</c>; one is picked by its goal's address
/// through the type (<c>app.test.Get("/Tests/x/Start")</c>). Holds what a run comes to (<see cref="Report"/>)
/// and whether one is running: the app is testing while a <see cref="Session"/> is open. How a run runs is
/// test's setting (<c>%!app.test.setting%</c>), read where it's needed.
/// </summary>
public sealed class @this : global::app.type.item.list.@this<global::app.test.@this>,
    global::app.type.item.setting.ISetting<global::app.test.setting.@this>
{
    private readonly global::app.@this _app;

    public @this(global::app.@this app) : base(new List<object?>())
    {
        _app = app;
        Report = new(this);
    }

    /// <summary>What the run comes to.</summary>
    public global::app.test.report.@this Report { get; }

    /// <summary>The run's open session, on whichever of the app's actors it was opened; null when the app is
    /// not testing. The one answer to "are we testing".</summary>
    public global::app.channel.type.test.@this? Session
        => _app.actor.list.Items().Select(a => a.Channel[typeof(global::app.channel.type.test.@this)])
            .OfType<global::app.channel.type.test.@this>().FirstOrDefault();

    /// <summary>Opens a session on the actor test's setting names: the run's own, or (given its test) the
    /// one a test's App writes into.</summary>
    public global::app.channel.type.test.@this Open(global::app.test.@this? test = null)
    {
        var session = new global::app.channel.type.test.@this(test);
        Actor.Channel.Register(session);
        return session;
    }

    /// <summary>Closes the open session, if there is one.</summary>
    public async Task Close()
    {
        if (Session is { } session) await session.Actor.Channel.RemoveAsync(session.Name);
    }

    // The actor test's setting names — one of the app's, which the actor list holds.
    private global::app.actor.@this Actor
    {
        get
        {
            var named = _app.System.Context.Setting.Of<global::app.test.setting.@this>().Actor.ToString();
            return _app.actor.list.Items().First(a => string.Equals(a.Name, named, StringComparison.OrdinalIgnoreCase));
        }
    }

    /// <summary>The App this run belongs to — reporters read its version for drift.</summary>
    internal global::app.@this App => _app;

    /// <summary>Fires once per test's App after it is made and configured (its OsDirectory, its session
    /// open), before the test's goal runs — a probe snapshots the test's App here. Parallel tests fire it
    /// concurrently.</summary>
    internal event System.Action<global::app.@this>? Made;

    /// <summary>
    /// Runs <paramref name="tests"/>, as many at once as test's setting says (≤ 0: one per processor), each
    /// under its timeout (≤ 0: none) in an App of its own — the file is the App boundary. A test that isn't
    /// Ready is recorded, not run. A test's failure is its outcome, never the run's: the others run on.
    /// Answers the tests, each carrying its outcome; each is added to this list and its coverage merged
    /// into this run's.
    /// </summary>
    public async Task<global::app.type.item.list.@this<global::app.test.@this>> Start(
        global::app.type.item.list.@this<global::app.test.@this> given, actor.context.@this context)
    {
        var tests = new List<global::app.test.@this>();
        foreach (var row in given.Items(context))
            if (await row.Value() is global::app.test.@this test) tests.Add(test);
        var executed = new global::app.type.item.list.@this<global::app.test.@this>(tests);
        if (tests.Count == 0) return executed;

        var setting = context.Setting.Of<global::app.test.setting.@this>();
        var seconds = setting.TimeoutSeconds.ToDouble();
        var timeout = seconds <= 0 ? System.Threading.Timeout.InfiniteTimeSpan : TimeSpan.FromSeconds(seconds);
        var parallel = setting.Parallel.ToInt32();
        if (parallel < 1) parallel = System.Environment.ProcessorCount;

        using var semaphore = new SemaphoreSlim(parallel);
        await Task.WhenAll(tests.Select(async test =>
        {
            await semaphore.WaitAsync(context.CancellationToken);
            try { await Run(test, timeout, context); }
            finally { semaphore.Release(); }
        }));
        return executed;
    }

    // One test: a test that isn't Ready is recorded, not run; a Ready one runs itself in an App of its
    // own, a child of this one, testing it (its session open) — the App lives exactly as long as the run.
    private async Task Run(global::app.test.@this test, TimeSpan timeout, actor.context.@this context)
    {
        if (test.Status == Status.Ready)
        {
            await using var app = new global::app.@this(_app);
            app.test.list.Open(test);
            Made?.Invoke(app);
            await test.Start(app, timeout, context);
            Report.Coverage.Merge(test.Coverage!);
        }
        Add(test);
    }

    /// <summary>The test a test goal is, as this run takes it. Its tags are the goal's own
    /// (<c>goal.Tag</c>, stamped at build) and the capabilities every action it reaches requires —
    /// its own and those of each goal its calls reach, as they are now. Seeds this run's coverage with
    /// the same goals. A goal tagged <c>skip</c> is Skipped; a test this run's tag filter leaves out
    /// is Skipped with the filter's reason; otherwise Ready.</summary>
    public async Task<global::app.test.@this> Create(global::app.goal.@this goal, actor.context.@this context)
    {
        var test = new global::app.test.@this { Goal = goal };
        test.Tags.Add(goal.Tag);

        var reached = new List<global::app.goal.@this> { goal };
        reached.AddRange(await goal.Callee(context));
        foreach (var g in reached)
        {
            foreach (var step in g.Step.Items())
                foreach (var action in step.Code.Items())
                    foreach (var required in action.Requirement)
                        if (global::app.type.item.tag.@this.Create(required) is { } tag) test.Tags.Add(tag);
            Report.Coverage.Add(g);
        }

        if (goal.Tag.Has(new global::app.type.item.tag.@this("skip")))
        {
            test.Status = Status.Skipped;
            test.StatusReason = "tagged 'skip'";
        }
        else if (Exclusion(test, context) is { } reason)
        {
            test.Status = Status.Skipped;
            test.StatusReason = reason;
        }
        return test;
    }

    /// <summary>Why this run leaves <paramref name="test"/> out — a tag in the setting's exclude (exclude
    /// wins), or no tag in a non-empty include. Null when the run takes it. Tags compare by the tag's own
    /// equality.</summary>
    public global::app.type.item.text.@this? Exclusion(global::app.test.@this test, actor.context.@this context)
    {
        var tags = test.Tags.Items().ToHashSet();
        var setting = context.Setting.Of<global::app.test.setting.@this>();
        // Each filter row is taken out as a value (a list set from the CLI holds its raw rows).
        bool Carries(global::app.type.item.list.@this<global::app.type.item.text.@this> filter)
            => filter.Items(context).Any(row => global::app.type.item.tag.@this.Create(row.Peek()) is { } tag && tags.Contains(tag));

        if (setting.Exclude.CountRaw > 0 && Carries(setting.Exclude)) return "excluded by tag";
        if (setting.Include.CountRaw > 0 && !Carries(setting.Include)) return "no include match";
        return null;
    }
}
