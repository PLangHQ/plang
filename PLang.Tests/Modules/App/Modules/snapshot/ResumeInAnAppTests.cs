using Identity = global::app.module.identity.Identity;

namespace PLang.Tests.App.Modules.snapshot;

/// <summary>
/// A resume goes on in an app of its own: a sub app the asker's actor makes, the snapshot's parts started into it, the
/// run going on there. The step answers what the run answers — its result, its error with a callback that still
/// resumes, or, not waited for, a task of it — and the asker's own variables and calls are never touched. Every resume
/// comes through the one door, so a callback is checked as a resume is.
/// </summary>
public class ResumeInAnAppTests
{
    // An app that signs for real (ed25519) as the shared test identity, or as the provider given.
    private static global::app.@this App(global::app.module.identity.code.IIdentity? identities = null)
    {
        var app = new global::app.@this(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "plang-resume-" + System.Guid.NewGuid().ToString("N")[..8]),
            global::PLang.Tests.Parent.Actor);
        app.test.list.Open(app.actor.list.User);
        app.Code.Register(identities ?? new global::PLang.Tests.Shared.TestIdentity { IsBuiltIn = true });
        app.Code.SetDefault<global::app.module.identity.code.IIdentity>(identities?.Name ?? "test-identity");
        return app;
    }

    // Goal G with the steps written, saved where its step at stop stands, after %note% was set.
    private static async Task<global::app.data.@this> Saved(global::app.@this app, int stop, params string[] lines)
    {
        var context = app.actor.list.User.Context;
        var goal = new Goal(app.type.list) { Name = "G", Path = global::app.type.item.path.@this.Resolve("/G.goal", context) };
        for (var index = 0; index < lines.Length; index++)
        {
            var step = new Step(app.type.list) { Goal = goal, Index = index, Text = lines[index] };
            step.Code.Add((await context.Action(lines[index])).In(step));
            goal.Step.Add(step);
        }
        app.goal.list.Add(goal);
        await context.Variable.Set("note", "original");
        // as a run stands in it: the goal's frame, its step's, then its action's
        await using var running = context.call.Push(goal, await context.call.Setting());
        await using var frame = context.call.Push(goal.Step[stop], await context.call.Setting());
        await using var call = context.call.Push(goal.Step[stop].Code[0], await context.call.Setting(), context.Variable);
        var saved = await new global::app.module.snapshot.save(context).Start();
        await call.DisposeAsync();
        await frame.DisposeAsync();
        await running.DisposeAsync();
        return saved;
    }

    private static async Task<global::app.data.@this> Resume(global::app.data.@this snapshot, global::app.actor.context.@this context,
        string with = "")
    {
        await context.Variable.Set("snap", snapshot);
        return await (await context.Action($"snapshot.resume(Snapshot=%snap%{with})")).Start(context);
    }

    private static async Task<string?> Saw(global::app.data.@this result, string name)
    {
        await result.IsSuccess();
        return (await (await result.Get(name)).Value())?.ToString();
    }

    [Test]
    public async Task Waited_AnswersWhatTheRunAnswers_AndTheAskerIsUntouched()
    {
        await using var app = App();
        var context = app.actor.list.User.Context;
        var saved = await Saved(app, 1, "variable.set(Name=%s0%, Value=\"first\")", "variable.set(Name=%s1%, Value=\"second\")",
            "goal.return(Data={\"s1\": %s1%, \"note\": %note%})");
        await context.Variable.Set("note", "the asker's");
        var current = context.call.Current;

        var result = await Resume(saved, context);

        await Assert.That(await Saw(result, "s1")).IsEqualTo("second");
        await Assert.That(await Saw(result, "note")).IsEqualTo("original");
        await Assert.That(await context.Variable.GetValue("note")).IsEqualTo("the asker's");
        await Assert.That((await context.Variable.Get("s1")).IsInitialized).IsFalse();
        await Assert.That(context.call.Current).IsSameReferenceAs(current);
    }

    // A run's return ends that run, as a goal call's callee's does: the asker's step after the resume goes on.
    [Test]
    public async Task ARunThatReturns_LeavesTheAskersNextStepToRun()
    {
        await using var app = App();
        var context = app.actor.list.User.Context;
        var saved = await Saved(app, 1, "variable.set(Name=%s0%, Value=\"first\")", "variable.set(Name=%s1%, Value=\"second\")",
            "goal.return(Data=%s1%)");
        await context.Variable.Set("snap", saved);
        var outer = new Goal(app.type.list) { Name = "Outer", Path = global::app.type.item.path.@this.Resolve("/Outer.goal", context) };
        foreach (var (index, line) in new[] { (0, "snapshot.resume(Snapshot=%snap%)"), (1, "variable.set(Name=%after%, Value=\"went on\")") })
        {
            var step = new Step(app.type.list) { Goal = outer, Index = index, Text = line };
            step.Code.Add((await context.Action(line)).In(step));
            outer.Step.Add(step);
        }
        app.goal.list.Add(outer);

        var result = await outer.Start(context, new List<global::app.data.@this>());

        await result.IsSuccess();
        await Assert.That(result.Returned).IsFalse();
        await Assert.That(await context.Variable.GetValue("after")).IsEqualTo("went on");
    }

    // A run saved inside a goal another called goes on through its caller as the call would have: the callee's answer
    // comes out of the call, the last thing its caller does, so it is the caller's answer and the run's.
    [Test]
    public async Task ARunSavedInACalledGoal_HandsTheCalleesAnswerToItsCaller()
    {
        await using var app = App();
        var context = app.actor.list.User.Context;
        async Task<Goal> Goal(string name, params string[] lines)
        {
            var goal = new Goal(app.type.list) { Name = name, Path = global::app.type.item.path.@this.Resolve($"/{name}.goal", context) };
            for (var index = 0; index < lines.Length; index++)
            {
                var step = new Step(app.type.list) { Goal = goal, List = goal.Step, Index = index, Text = lines[index] };
                var read = await new global::app.goal.step.action.formal.Reader(step, app.module.list).Read(lines[index], context);
                await read.IsSuccess();
                foreach (var action in ((global::app.goal.step.action.list.@this)read.Peek()).Items()) step.Code.Add(action);
                step.Code.Attach();
                goal.Step.Add(step);
            }
            app.goal.list.Add(goal);
            return goal;
        }
        var inner = await Goal("Inner", "variable.set(Name=%s0%, Value=\"read\")", "goal.return(Data=%s0%)");
        var outer = await Goal("Outer", "goal.call(Name=\"Inner\")");
        var setting = await context.call.Setting();
        global::app.data.@this saved;
        await using (context.call.Push(outer, setting))
        await using (context.call.Push(outer.Step[0], setting))
        await using (context.call.Push(outer.Step[0].Code[0], setting, context.Variable))
        await using (context.call.Push(inner, setting))
        await using (context.call.Push(inner.Step[0], setting))
        await using (context.call.Push(inner.Step[0].Code[0], setting, context.Variable))
            saved = await new global::app.module.snapshot.save(context).Start();

        var result = await Resume(saved, context);

        await result.IsSuccess();
        await Assert.That((await result.Value())?.ToString()).IsEqualTo("read");
    }

    [Test]
    public async Task NotWaited_AnswersATask_WhoseWaitGivesTheResult()
    {
        await using var app = App();
        var context = app.actor.list.User.Context;
        var saved = await Saved(app, 1, "variable.set(Name=%s0%, Value=\"first\")", "variable.set(Name=%s1%, Value=\"second\")",
            "goal.return(Data={\"s1\": %s1%})");

        var result = await Resume(saved, context, ", Parallel=true");

        await result.IsSuccess();
        var task = await result.Value<global::app.task.@this>();
        await Assert.That(task).IsNotNull();
        await Assert.That(task!.Goal.Name).IsEqualTo("G");
        await Assert.That(await Saw(await task.Wait(), "s1")).IsEqualTo("second");
    }

    // A run that fails answers its error, all the way up; the error holds its callback, taken while the run still
    // stood, so after the run's app is gone the callback still goes on (and meets the same failure again).
    [Test]
    public async Task AFailedRun_AnswersItsError_ItsCallbackStillResumes()
    {
        await using var app = App();
        var context = app.actor.list.User.Context;
        var saved = await Saved(app, 1, "variable.set(Name=%s0%, Value=\"first\")", "error.throw(Message=\"boom\", Key=\"Boom\")");

        var result = await Resume(saved, context);

        await result.IsFailure();
        await Assert.That(result.Error!.Key).IsEqualTo("Boom");
        var callback = await result.Error.Callback(context);
        await callback.IsSuccess();
        var again = await new global::app.goal.step.action.composed.@this(
            new global::app.module.callback.start(context) { Callback = callback }, context).Start(context);
        await again.IsFailure();
        await Assert.That(again.Error!.Key).IsEqualTo("Boom");
    }

    [Test]
    public async Task TwoResumes_OfOneSnapshot_AreTwoApps()
    {
        await using var app = App();
        var context = app.actor.list.User.Context;
        var saved = await Saved(app, 1, "variable.set(Name=%s0%, Value=\"first\")", "variable.set(Name=%s1%, Value=\"second\")",
            "goal.return(Data=%!app%)");

        var first = await (await Resume(saved, context)).Value();
        var second = await (await Resume(saved, context)).Value();

        await Assert.That(first).IsTypeOf<global::app.@this>();
        await Assert.That(first).IsNotSameReferenceAs(second);
        await Assert.That(first).IsNotSameReferenceAs(app);
    }

    // start %callback% goes through the resume door: a snapshot another identity signed is refused, as a resume of it is.
    [Test]
    public async Task ACallbackSignedByAnotherIdentity_IsRefused()
    {
        await using var other = App(new Keys("other"));
        var forged = await Saved(other, 1, "variable.set(Name=%s0%, Value=\"first\")", "variable.set(Name=%s1%, Value=\"second\")");

        await using var app = App();
        var context = app.actor.list.User.Context;
        var result = await new global::app.goal.step.action.composed.@this(
            new global::app.module.callback.start(context) { Callback = forged }, context).Start(context);

        await result.IsFailure();
        await Assert.That(result.Error!.Key).IsEqualTo("SnapshotNotTheApps");
    }

    // A run whose error handler starts that error's callback meets the same error, and the same handler, in the app it
    // goes on in — without end. Each app is made under the last, so past the deepest an app may be the making is
    // refused: the run answers AppTooDeep instead of the thread's stack giving out.
    [Test]
    public async Task ACallbackThatResumesItselfWithoutEnd_AnswersAppTooDeep()
    {
        await using var app = App();
        var context = app.actor.list.User.Context;
        async Task<Goal> Goal(string name, params string[] lines)
        {
            var goal = new Goal(app.type.list) { Name = name, Path = global::app.type.item.path.@this.Resolve($"/{name}.goal", context) };
            for (var index = 0; index < lines.Length; index++)
            {
                var step = new Step(app.type.list) { Goal = goal, List = goal.Step, Index = index, Text = lines[index] };
                var read = await new global::app.goal.step.action.formal.Reader(step, app.module.list).Read(lines[index], context);
                await read.IsSuccess();
                foreach (var action in ((global::app.goal.step.action.list.@this)read.Peek()).Items()) step.Code.Add(action);
                step.Code.Attach();
                goal.Step.Add(step);
            }
            app.goal.list.Add(goal);
            return goal;
        }
        var g = await Goal("G", "error.throw(Message=\"boom\", Key=\"Boom\"); on.error(Recovery=[variable.set(Name=%cb%, Value=%!error.callback%), callback.start(Callback=%cb%)])");

        var result = await g.Start(context, new List<global::app.data.@this>());

        await result.IsFailure();
        await Assert.That(Chain(result.Error!)).Contains("AppTooDeep");
    }

    // An error's key and every key under it.
    private static IEnumerable<string> Chain(global::app.error.@this error)
        => error.list.SelectMany(Chain).Prepend(error.Key);

    // An identity provider holding one keypair of its own, its default and only listed identity.
    private sealed class Keys : global::app.module.identity.code.IIdentity
    {
        private readonly (string PublicKey, string PrivateKey) _keys;
        public Keys(string name) { Name = name; _keys = Make(); }
        public Keys Archived() => new(Name + "-archived");

        private static (string, string) Make()
        {
            var (pair, _) = new global::app.module.signing.code.Ed25519().GenerateKeyPair();
            return (pair!.PublicKey, pair.PrivateKey);
        }

        public string Name { get; }
        public bool IsBuiltIn { get; set; } = true;
        public string? Source { get; set; }

        private Identity Mine(global::app.module.IContext a) => new("default", a.Context.App.type.list[typeof(Identity)])
            { PublicKey = _keys.PublicKey, PrivateKey = _keys.PrivateKey, IsDefault = true };
        private global::app.data.@this<Identity> Ok(global::app.module.IContext a) => a.Context.Ok<Identity>(Mine(a));

        public Task<global::app.data.@this<Identity>> GetAsync(global::app.module.identity.Get action) => Task.FromResult(Ok(action));
        public Task<global::app.data.@this<Identity>> GetOrCreateDefaultAsync(global::app.module.IContext action) => Task.FromResult(Ok(action));
        public Task<global::app.data.@this<Identity>> CreateAsync(global::app.module.identity.Create action) => Task.FromResult(Ok(action));
        public Task<global::app.data.@this<Identity>> ArchiveAsync(global::app.module.identity.Archive action) => Task.FromResult(Ok(action));
        public Task<global::app.data.@this<Identity>> UnarchiveAsync(global::app.module.identity.Unarchive action) => Task.FromResult(Ok(action));
        public Task<global::app.data.@this<Identity>> SetDefaultAsync(global::app.module.identity.SetDefault action) => Task.FromResult(Ok(action));
        public Task<global::app.data.@this<Identity>> RenameAsync(global::app.module.identity.Rename action) => Task.FromResult(Ok(action));
        public Task<global::app.data.@this<Identity>> ExportAsync(global::app.module.identity.Export action) => Task.FromResult(Ok(action));
        public Task<global::app.data.@this<global::app.type.item.list.@this<Identity>>> ListAsync(global::app.module.identity.list action)
            => Task.FromResult(action.Context.Ok<global::app.type.item.list.@this<Identity>>(new global::app.type.item.list.@this<Identity>(
                new global::app.data.@this[] { action.Context.Ok<Identity>(Mine(action)) },
                action.Context.App.type.list[typeof(global::app.type.item.list.@this<Identity>)])));
    }
}
