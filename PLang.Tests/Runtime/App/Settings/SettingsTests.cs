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
public class SettingsTests
{
    private global::app.actor.context.@this Ctx()
    {
        var engine = new EngineType("/app");
        return new global::app.actor.context.@this(engine, engine.User, new Variables(engine.User.Context));
    }

    [Test]
    public async Task Get_Unset_IsNotFound()
    {
        var ctx = Ctx();
        var d = await ctx.Setting.Get(new[] { "archive.max" });
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

        var result = app.System.Setting.Set("build", settings);
        await Assert.That(result.Success).IsTrue().Because(result.Error?.Message ?? "ok");

        // The consumer's read: each string row lifts to a REAL path (text→path via the lift door).
        var files = app.System.Context.Setting.Of<global::app.module.action.build.setting.@this>().Files;
        var paths = new List<global::app.type.item.path.@this>();
        foreach (var row in files.Items(global::PLang.Tests.TestApp.SharedContext))
            paths.Add((await row.Value<global::app.type.item.path.@this>())!);

        await Assert.That(paths.Count).IsEqualTo(2);
        await Assert.That(paths[0].ToString()).Contains("a.goal");
    }

    [Test]
    public async Task Set_ThenGet_ReturnsValue()
    {
        var ctx = Ctx();
        await ctx.Setting.Set("archive.max", ctx.Ok(42L));

        var d = await ctx.Setting.Get(new[] { "archive.max" });
        await Assert.That(d.IsInitialized).IsTrue();
        await Assert.That((await d.Value())?.ToString()).IsEqualTo("42");
    }

    [Test]
    public async Task Child_InheritsParentSetting()
    {
        var parent = Ctx();
        await parent.Setting.Set("archive.max", parent.Ok(50L));

        var child = parent.CreateChild();
        var d = await child.Setting.Get(new[] { "archive.max" });
        await Assert.That((await d.Value())?.ToString()).IsEqualTo("50");
    }

    [Test]
    public async Task Child_Shadows_ParentUnaffected()
    {
        var parent = Ctx();
        await parent.Setting.Set("archive.max", parent.Ok(50L));

        var child = parent.CreateChild();
        await child.Setting.Set("archive.max", child.Ok(10L));

        await Assert.That((await (await child.Setting.Get(new[] { "archive.max" })).Value())?.ToString()).IsEqualTo("10");
        await Assert.That((await (await parent.Setting.Get(new[] { "archive.max" })).Value())?.ToString()).IsEqualTo("50");
    }

    [Test]
    public async Task Clone_Isolates_Writes()
    {
        var ctx = Ctx();
        await ctx.Setting.Set("archive.max", ctx.Ok(42L));

        var clone = ctx.Setting.Clone();
        await clone.Set("archive.max", ctx.Ok(999L));

        await Assert.That((await (await clone.Get(new[] { "archive.max" })).Value())?.ToString()).IsEqualTo("999");
        await Assert.That((await (await ctx.Setting.Get(new[] { "archive.max" })).Value())?.ToString()).IsEqualTo("42");
    }

    // The user's settings fall back to the system's: a setting on the system reaches a user context.
    [Test]
    public async Task UserContext_FallsBackTo_TheSystemsSetting()
    {
        var engine = new EngineType("/app");
        await engine.System.Setting.Set("llm.cache", engine.System.Context.Ok(false));

        var read = await engine.User.Context.Setting.Get(new[] { "llm.cache" });
        await Assert.That((await read.Value())?.ToString()).IsEqualTo("false");
    }

    // The system does not see the user's: a setting on the user stays the user's.
    [Test]
    public async Task SystemContext_DoesNotSee_TheUsersSetting()
    {
        var engine = new EngineType("/app");
        await engine.User.Setting.Set("llm.cache", engine.User.Context.Ok(false));

        var read = await engine.System.Context.Setting.Get(new[] { "llm.cache" });
        await Assert.That(read.IsInitialized).IsFalse();
    }
}
