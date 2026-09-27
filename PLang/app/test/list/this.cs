using app.test;

namespace app.test.list;

/// <summary>
/// The run's tests — a list of them, reached at <c>app.test.list</c>; one is picked by its goal's address
/// through the type (<c>app.test.Get("/Tests/x/Start")</c>). Holds what a run comes to (<see cref="Report"/>)
/// and whether one is running: the app is testing while a <see cref="Session"/> is open. How a run runs is
/// test's setting (<c>%!app.test.setting%</c>), read where it's needed.
/// </summary>
public sealed class @this : global::app.type.item.list.@this<global::app.test.@this>
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
    /// Runs the tests <paramref name="given"/> holds, as many at once as test's setting says (≤ 0: one per
    /// processor), each running itself in an App of its own — the file is the App boundary. A test that isn't
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

        var parallel = context.Setting.Of<global::app.test.setting.@this>().Parallel.ToInt32();
        if (parallel < 1) parallel = System.Environment.ProcessorCount;

        using var semaphore = new SemaphoreSlim(parallel);
        await Task.WhenAll(tests.Select(async test =>
        {
            await semaphore.WaitAsync(context.CancellationToken);
            try { await Run(test, context); }
            finally { semaphore.Release(); }
        }));
        return executed;
    }

    // One test: this run's coverage takes in the goals it reaches (a site that never runs still shows); a test
    // that isn't Ready is recorded, not run; a Ready one runs itself in an App of its own, a child of this one,
    // testing it (its session open) — the App lives exactly as long as the run.
    private async Task Run(global::app.test.@this test, actor.context.@this context)
    {
        foreach (var goal in test.Reach) Report.Coverage.Add(goal);
        if (test.Status == Status.Ready)
        {
            await using var app = new global::app.@this(_app);
            app.test.list.Open(test);
            Made?.Invoke(app);
            await test.Start(app, context);
            Report.Coverage.Merge(app.test.list.Report.Coverage);
        }
        Add(test);
    }

}
