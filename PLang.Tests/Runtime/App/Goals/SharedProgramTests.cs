namespace PLang.Tests.App.Goals;

/// <summary>
/// The program graph is shared by every run. A run never writes to it: each run makes its own Data
/// from a property, born with the run's context, and a value loads with the context of the Data
/// that asks — so two actors running the same program each resolve in their own memory and check
/// their own permissions. Rows are read from a real .pr (the goal's own writer and reader).
/// </summary>
public class SharedProgramTests
{
    private global::app.@this _app = null!;

    [Before(Test)]
    public void Setup() => _app = TestApp.Create("/tmp/sharedprogram-" + System.Guid.NewGuid().ToString("N")[..8]);

    [After(Test)]
    public async Task Cleanup() => await _app.DisposeAsync();

    // A goal as a real .pr gives it: written by the goal's own writer, read back by its reader.
    private async Task<global::app.goal.@this> ReadFromPr(string name, params global::PLang.Tests.Shared.Make.StepDef[] steps)
        => await global::PLang.Tests.Shared.RealGoalLoad.ViaChannel(_app,
            global::PLang.Tests.Shared.Make.Goal(name, "/" + name + ".goal", steps));

    private async Task<global::app.goal.step.action.@this> ActionFromPr(string module, string action, params (string, object?)[] parameters)
        => (await ReadFromPr("Start", global::PLang.Tests.Shared.Make.Step("a step",
            global::PLang.Tests.Shared.Make.Action(module, action, parameters)))).Step[0].Code[0];

    // A variable-naming row, typed `variable` as the builder writes it into the .pr.
    private Data Var(string slot, string name) => new(slot, name, new global::app.type.@this("variable"), context: _app.User.Context);

    private async Task<T> Bound<T>(global::app.goal.step.action.@this action, global::app.actor.context.@this context) where T : class
    {
        var (handler, error) = await action.Bind(context);
        await Assert.That(error).IsNull();
        return (T)handler!;
    }

    [Test]
    public async Task PlainSlot_BoundUnderTwoActors_EachResolvesInItsOwnVariables()
    {
        await _app.System.Context.Variable.Set("x", "system");
        await _app.User.Context.Variable.Set("x", "user");
        var shared = await ActionFromPr("list", "add", ("ListName", Var("ListName", "l")), ("Value", "%x%"));

        // Two runs interleave: the System run binds, the User run binds, then the System run reads.
        var onSystem = await Bound<global::app.module.action.list.Add>(shared, _app.System.Context);
        var onUser = await Bound<global::app.module.action.list.Add>(shared, _app.User.Context);

        await Assert.That((await onSystem.Value.Value()).ToString()).IsEqualTo("system");
        await Assert.That((await onUser.Value.Value()).ToString()).IsEqualTo("user");
    }

    [Test]
    public async Task TypedSlot_BoundUnderTwoActors_EachResolvesInItsOwnVariables()
    {
        await _app.System.Context.Variable.Set("n", "system");
        await _app.User.Context.Variable.Set("n", "user");
        var shared = await ActionFromPr("list", "split", ("Value", "%n%"));

        var onSystem = await Bound<global::app.module.action.list.Split>(shared, _app.System.Context);
        var onUser = await Bound<global::app.module.action.list.Split>(shared, _app.User.Context);

        await Assert.That((await onSystem.Value.Value())!.ToString()).IsEqualTo("system");
        await Assert.That((await onUser.Value.Value())!.ToString()).IsEqualTo("user");
    }

    [Test]
    public async Task LiteralList_VariableElement_ResolvesPerRun()
    {
        await _app.System.Context.Variable.Set("x", "system");
        await _app.User.Context.Variable.Set("x", "user");
        var shared = await ActionFromPr("list", "add", ("ListName", Var("ListName", "l")), ("Value", new List<object?> { "%x%" }));

        var onSystem = await Bound<global::app.module.action.list.Add>(shared, _app.System.Context);
        var onUser = await Bound<global::app.module.action.list.Add>(shared, _app.User.Context);
        var systemList = (global::app.type.item.list.@this)(await onSystem.Value.Value())!;
        var userList = (global::app.type.item.list.@this)(await onUser.Value.Value())!;

        await Assert.That((await systemList.Items(global::PLang.Tests.TestApp.SharedContext).ElementAt(0).Value()).ToString()).IsEqualTo("system");
        await Assert.That((await userList.Items(global::PLang.Tests.TestApp.SharedContext).ElementAt(0).Value()).ToString()).IsEqualTo("user");
    }

    [Test]
    public async Task LiteralDict_VariableElement_ResolvesPerRun()
    {
        await _app.System.Context.Variable.Set("x", "system");
        await _app.User.Context.Variable.Set("x", "user");
        var shared = await ActionFromPr("list", "add", ("ListName", Var("ListName", "l")), ("Value", new Dictionary<string, object?> { ["k"] = "%x%" }));

        var onSystem = await Bound<global::app.module.action.list.Add>(shared, _app.System.Context);
        var onUser = await Bound<global::app.module.action.list.Add>(shared, _app.User.Context);

        await Assert.That((await (await onSystem.Value.Get("k")).Value()).ToString()).IsEqualTo("system");
        await Assert.That((await (await onUser.Value.Get("k")).Value()).ToString()).IsEqualTo("user");
    }

    [Test]
    public async Task Binding_NeverWritesTheSharedProperty()
    {
        await _app.User.Context.Variable.Set("x", "user");
        var shared = await ActionFromPr("list", "add", ("ListName", Var("ListName", "l")), ("Value", "%x%"));
        var property = shared["Value"]!;
        var held = property.Value;

        var bound = await Bound<global::app.module.action.list.Add>(shared, _app.User.Context);
        await bound.Value.Value();

        await Assert.That(ReferenceEquals(bound.Value.Context, _app.User.Context)).IsTrue();
        await Assert.That(ReferenceEquals(property.Value, held)).IsTrue();
    }

    [Test]
    public async Task OneGoal_RunConcurrentlyByTwoActors_EachWritesItsOwnMemory()
    {
        await _app.System.Context.Variable.Set("x", "system");
        await _app.User.Context.Variable.Set("x", "user");
        var goal = await ReadFromPr("Start", global::PLang.Tests.Shared.Make.Step("add x",
            global::PLang.Tests.Shared.Make.Action("list", "add", ("ListName", Var("ListName", "l")), ("Value", "%x%"))));

        // Each round, System and User run the same goal at the same time (within an actor, runs are serial).
        for (int i = 0; i < 25; i++)
            foreach (var run in await Task.WhenAll(goal.Run(_app.System.Context), goal.Run(_app.User.Context)))
                await run.IsSuccess();

        async Task<List<string>> Seen(global::app.actor.context.@this ctx)
        {
            var list = (global::app.type.item.list.@this)(await (await ctx.Variable.Get("l")).Value())!;
            var seen = new List<string>();
            foreach (var row in list.Items(global::PLang.Tests.TestApp.SharedContext)) seen.Add((await row.Value()).ToString()!);
            return seen;
        }
        var system = await Seen(_app.System.Context);
        var user = await Seen(_app.User.Context);

        await Assert.That(system.Count).IsEqualTo(25);
        await Assert.That(user.Count).IsEqualTo(25);
        await Assert.That(system.All(s => s == "system")).IsTrue();
        await Assert.That(user.All(s => s == "user")).IsTrue();
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task OneGoal_RunConcurrentlyBySameActor_EveryAddLands(bool listExists)
    {
        const int n = 500;
        await _app.User.Context.Variable.Set("x", "user");
        if (listExists) await _app.User.Context.Variable.Set("l", new global::app.type.item.list.@this());
        var goal = await ReadFromPr("Start", global::PLang.Tests.Shared.Make.Step("add x",
            global::PLang.Tests.Shared.Make.Action("list", "add", ("ListName", Var("ListName", "l")), ("Value", "%x%"))));

        var runs = await Task.WhenAll(Enumerable.Range(0, n).Select(_ => Task.Run(() => goal.Run(_app.User.Context))));
        foreach (var run in runs)
            await Assert.That(run.Success).IsTrue().Because(run.Error?.Exception?.ToString() ?? run.Error?.Message ?? "");

        var list = (global::app.type.item.list.@this)(await (await _app.User.Context.Variable.Get("l")).Value())!;
        await Assert.That(list.Items(_app.User.Context).Count()).IsEqualTo(n);
    }

    // A literal file read by two actors: the one holding the grant reads it, the other is denied
    // (never served the first run's content), and a later run reads the file as it is now.
    [Test]
    public async Task LiteralFile_TwoActors_DeniedWithoutGrant_FreshAfterChange()
    {
        var dir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "plang-shared-" + System.Guid.NewGuid().ToString("N")[..8]);
        System.IO.Directory.CreateDirectory(dir);
        var file = System.IO.Path.Combine(dir, "data.txt");
        System.IO.File.WriteAllText(file, "v1");
        var grant = new global::app.type.item.permission.@this(
            Actor: _app.User.Name,
            Path: file,
            Verbs: new System.Collections.Generic.HashSet<global::app.type.item.permission.Verb> { global::app.type.item.permission.Verb.Read },
            Match: global::app.type.item.permission.Match.Exact);
        await _app.User.Permission.Add(new global::app.data.@this<global::app.type.item.permission.@this>("", grant, context: _app.User.Context), persist: false);
        // Out of the app root: the root and the file both sit under the temp folder.
        var read = await ActionFromPr("file", "read", ("Path", "../" + System.IO.Path.GetFileName(dir) + "/data.txt"));

        var first = await read.Run(_app.User.Context);
        await first.IsSuccess();
        var firstContent = (await first.Value())!.ToString();

        var denied = await read.Run(_app.System.Context);
        var deniedContent = denied.Success ? (await denied.Value())?.ToString() : null;

        System.IO.File.WriteAllText(file, "v2");
        var again = await read.Run(_app.User.Context);
        await again.IsSuccess();
        var againContent = (await again.Value())!.ToString();

        await Assert.That(firstContent).IsEqualTo("v1");
        await Assert.That(deniedContent).IsNull();
        await Assert.That(denied.Error?.Key).IsEqualTo("PermissionDenied");
        await Assert.That(againContent).IsEqualTo("v2");
    }

    // A cross-actor goal.call: the argument loads from the CALLER's memory even though the callee
    // (System) reads it, and the shared row never enters the callee's variables.
    [Test]
    public async Task CrossActorGoalCall_ArgumentLoadsFromTheCaller_RowUnchanged()
    {
        await _app.User.Context.Variable.Set("city", "Reykjavik");
        await _app.System.Context.Variable.Set("city", "Nowhere");
        var callee = await ReadFromPr("Weather", global::PLang.Tests.Shared.Make.Step("remember the city",
            global::PLang.Tests.Shared.Make.Action("variable", "set",
                ("Name", Var("Name", "seen")),
                ("Value", "%place%"))));
        _app.Goal.Add(callee);
        var call = await ActionFromPr("goal", "call", ("Name", "Weather"), ("Actor", "system"),
            // an argument row the programmer wrote with a %variable% is marked on its row, as the builder writes it
            ("Parameter", new List<object?> { new Data("place", "%city%", new global::app.type.@this("text", template: "plang"), context: _app.User.Context) }));
        var property = call["Parameter"]!;
        var held = property.Value;

        var result = await call.Run(_app.User.Context);

        await result.IsSuccess();
        await Assert.That((await (await _app.System.Context.Variable.Get("seen")).Value()).ToString()).IsEqualTo("Reykjavik");
        // The property still holds the same unloaded value — the call read its own Data.
        await Assert.That(ReferenceEquals(property.Value, held)).IsTrue();
    }
}
