using app;
using app.type.item.variable;
using EngineType = global::app.@this;

namespace PLang.Tests.App.Settings;

/// <summary>
/// The in-memory setting cascade (<c>app.actor.setting.@this</c>): scope shadowing (context → parent →
/// the actor's → the system's) and clone isolation. Type conversion and the
/// <c>[Default]</c> fallback moved onto the generator seam (exercised by the action tests), so
/// these tests assert scope resolution only, in Data terms.
/// </summary>
public class SettingsTests : System.IAsyncDisposable
{
    private readonly global::app.@this app = new global::app.@this("/app").Testing();
    public async System.Threading.Tasks.ValueTask DisposeAsync() => await app.DisposeAsync();

    private global::app.actor.context.@this Ctx()
    {
        var engine = new EngineType("/app");
        return new global::app.actor.context.@this(engine, engine.actor.list.User, new Variables(engine.actor.list.User.Context));
    }

    // The action an option is read for — the settings build its keys from the module's and the action's names.
    private global::app.goal.step.action.@this Request(global::app.actor.setting.@this _)
        => app.actor.list.User.Context.App.Module("http")["request"]!;

    private global::app.goal.step.action.@this Query(EngineType engine) => engine.Module("llm")["query"]!;

    [Test]
    public async Task Get_Unset_IsNotFound()
    {
        var ctx = Ctx();
        var d = await ctx.Setting.Get(Request(ctx.Setting), "timeout");
        await Assert.That(d.IsInitialized).IsFalse();   // unset → NotFound → the seam falls to [Default]
    }

    // --build={"files":["a.goal"]} binds a JSON string array to build's files setting (a plang list);
    // each row lifts to a REAL path at its door (row.Value<path>()).
    [Test]
    public async Task Set_StringArray_BindsToListOfPath()
    {
        await using var app = new EngineType("/app");
        var settings = new Dictionary<string, object?>
        {
            ["files"] = new List<object?> { "a.goal", "b.goal" },
        };

        var result = app.actor.list.System.Setting.Set("build.setting", settings);
        await Assert.That(result.Success).IsTrue().Because(result.Error?.Message ?? "ok");

        // The consumer's read: each string row lifts to a REAL path (text→path via the lift door).
        var files = app.actor.list.System.Context.Setting.Of<global::app.module.build.setting.@this>().Files;
        var paths = new List<global::app.type.item.path.@this>();
        foreach (var row in files.Items(app.actor.list.User.Context))
            paths.Add((await row.Value<global::app.type.item.path.@this>())!);

        await Assert.That(paths.Count).IsEqualTo(2);
        await Assert.That(paths[0].ToString()).Contains("a.goal");
    }

    [Test]
    public async Task Set_ThenGet_ReturnsValue()
    {
        var ctx = Ctx();
        await ctx.Setting.Set("http.request.setting.timeout", ctx.Ok(42L));

        var d = await ctx.Setting.Get(Request(ctx.Setting), "timeout");
        await Assert.That(d.IsInitialized).IsTrue();
        await Assert.That((await d.Value())?.ToString()).IsEqualTo("42");
    }

    [Test]
    public async Task Child_InheritsParentSetting()
    {
        var parent = Ctx();
        await parent.Setting.Set("http.request.setting.timeout", parent.Ok(50L));

        var child = parent.CreateChild();
        var d = await child.Setting.Get(Request(child.Setting), "timeout");
        await Assert.That((await d.Value())?.ToString()).IsEqualTo("50");
    }

    [Test]
    public async Task Child_Shadows_ParentUnaffected()
    {
        var parent = Ctx();
        await parent.Setting.Set("http.request.setting.timeout", parent.Ok(50L));

        var child = parent.CreateChild();
        await child.Setting.Set("http.request.setting.timeout", child.Ok(10L));

        await Assert.That((await (await child.Setting.Get(Request(child.Setting), "timeout")).Value())?.ToString()).IsEqualTo("10");
        await Assert.That((await (await parent.Setting.Get(Request(parent.Setting), "timeout")).Value())?.ToString()).IsEqualTo("50");
    }

    [Test]
    public async Task Clone_Isolates_Writes()
    {
        var ctx = Ctx();
        await ctx.Setting.Set("http.request.setting.timeout", ctx.Ok(42L));

        var clone = ctx.Setting.Clone();
        await clone.Set("http.request.setting.timeout", ctx.Ok(999L));

        await Assert.That((await (await clone.Get(Request(clone), "timeout")).Value())?.ToString()).IsEqualTo("999");
        await Assert.That((await (await ctx.Setting.Get(Request(ctx.Setting), "timeout")).Value())?.ToString()).IsEqualTo("42");
    }

    // The user's settings fall back to the system's: a setting on the system reaches a user context.
    [Test]
    public async Task UserContext_FallsBackTo_TheSystemsSetting()
    {
        var engine = new EngineType("/app");
        await engine.actor.list.System.Setting.Set("llm.setting.cache", engine.actor.list.System.Context.Ok(false));

        var read = await engine.actor.list.User.Context.Setting.Get(Query(engine), "cache");
        await Assert.That((await read.Value())?.ToString()).IsEqualTo("false");
    }

    // The system does not see the user's: a setting on the user stays the user's.
    [Test]
    public async Task SystemContext_DoesNotSee_TheUsersSetting()
    {
        var engine = new EngineType("/app");
        await engine.actor.list.User.Setting.Set("llm.setting.cache", engine.actor.list.User.Context.Ok(false));

        var read = await engine.actor.list.System.Context.Setting.Get(Query(engine), "cache");
        await Assert.That(read.IsInitialized).IsFalse();
    }
}
