using app.test;

namespace app.test.list;

/// <summary>
/// The run's tests — a list of them, reached at <c>app.test.list</c>; one is picked by its goal's address
/// through the type (<c>app.test.Get("/Tests/x/Start")</c>). Holds how a run runs (<see cref="Setting"/>),
/// what it comes to (<see cref="Report"/>), and whether one is running: the app is testing while its
/// <see cref="Session"/> is open.
/// </summary>
public sealed class @this : global::app.type.item.list.@this<global::app.test.@this>
{
    private readonly global::app.@this _app;

    public @this(global::app.@this app) : base(new List<object?>())
    {
        _app = app;
        Report = new(this);
    }

    /// <summary>How a run runs — <c>--test={…}</c> applies onto it.</summary>
    public global::app.test.setting.@this Setting { get; } = new();

    /// <summary>What the run comes to.</summary>
    public global::app.test.report.@this Report { get; }

    /// <summary>The run's open session on the actor its setting names; null when the app is not testing.
    /// The one answer to "are we testing".</summary>
    public global::app.channel.type.test.@this? Session
        => Actor.Channel[typeof(global::app.channel.type.test.@this)] as global::app.channel.type.test.@this;

    /// <summary>Opens a session on the setting's actor: the run's own, or (given its test) the one a
    /// test's App writes into.</summary>
    public global::app.channel.type.test.@this Open(global::app.test.@this? test = null)
    {
        var session = new global::app.channel.type.test.@this(test);
        Actor.Channel.Register(session);
        return session;
    }

    /// <summary>Closes the open session, if there is one.</summary>
    public async Task Close()
    {
        if (Session is { } session) await Actor.Channel.RemoveAsync(session.Name);
    }

    // The actor the setting names — one of the app's, which the actor list holds.
    private global::app.actor.@this Actor
        => _app.actor.list.Items().First(a => string.Equals(a.Name, Setting.Actor.ToString(), StringComparison.OrdinalIgnoreCase));

    /// <summary>The App this run belongs to — reporters read its version for drift.</summary>
    internal global::app.@this App => _app;

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
        // Each filter row is taken out as a value (a list set from the CLI holds its raw rows).
        bool Carries(global::app.type.item.list.@this<global::app.type.item.text.@this> filter)
            => filter.Items(context).Any(row => global::app.type.item.tag.@this.Create(row.Peek()) is { } tag && tags.Contains(tag));

        if (Setting.Exclude.CountRaw > 0 && Carries(Setting.Exclude)) return "excluded by tag";
        if (Setting.Include.CountRaw > 0 && !Carries(Setting.Include)) return "no include match";
        return null;
    }
}
