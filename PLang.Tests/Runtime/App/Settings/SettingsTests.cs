using app;
using app.variable;
using EngineType = global::app.@this;
using Storage = global::app.setting.Storage;

namespace PLang.Tests.App.Settings;

/// <summary>
/// The in-memory setting cascade on the unified <c>app.Setting</c> (<c>app.setting.@this</c>):
/// scope shadowing (context → parent → app root) and clone isolation. Type conversion and the
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
        var d = await ctx.Setting.Get(Storage.InMemory, "archive.max");
        await Assert.That(d.IsInitialized).IsFalse();   // unset → NotFound → the seam falls to [Default]
    }

    // --build={"files":["a.goal"]} binds a JSON string array to Build.Files (now a plang list). The
    // walk stores it lazily; each row lifts to a REAL path at its door (row.Value<path>()). Repro of
    // the --build={...} crash — the CLR List<path> that forced set-time cross-family conversion.
    [Test]
    public async Task Set_StringArray_BindsToListOfPath()
    {
        await using var app = new EngineType("/app");
        var node = new global::app.module.action.build.@this(app.System.Context);
        var settings = new Dictionary<string, object?>
        {
            ["files"] = new List<object?> { "a.goal", "b.goal" },
        };

        var result = app.Setting.Set(node, settings);
        await Assert.That(result.Success).IsTrue().Because(result.Error?.Message ?? "ok");

        // The consumer's read: each string row lifts to a REAL path (text→path via the lift door).
        var paths = new List<global::app.type.item.path.@this>();
        foreach (var row in node.Files)
            paths.Add((await row.Value<global::app.type.item.path.@this>())!);

        await Assert.That(paths.Count).IsEqualTo(2);
        await Assert.That(paths[0].ToString()).Contains("a.goal");
    }

    [Test]
    public async Task Set_ThenGet_ReturnsValue()
    {
        var ctx = Ctx();
        await ctx.Setting.Set(Storage.InMemory, "archive.max", ctx.Ok(42L));

        var d = await ctx.Setting.Get(Storage.InMemory, "archive.max");
        await Assert.That(d.IsInitialized).IsTrue();
        await Assert.That((await d.Value())?.ToString()).IsEqualTo("42");
    }

    [Test]
    public async Task Child_InheritsParentSetting()
    {
        var parent = Ctx();
        await parent.Setting.Set(Storage.InMemory, "archive.max", parent.Ok(50L));

        var child = parent.CreateChild();
        var d = await child.Setting.Get(Storage.InMemory, "archive.max");
        await Assert.That((await d.Value())?.ToString()).IsEqualTo("50");
    }

    [Test]
    public async Task Child_Shadows_ParentUnaffected()
    {
        var parent = Ctx();
        await parent.Setting.Set(Storage.InMemory, "archive.max", parent.Ok(50L));

        var child = parent.CreateChild();
        await child.Setting.Set(Storage.InMemory, "archive.max", child.Ok(10L));

        await Assert.That((await (await child.Setting.Get(Storage.InMemory, "archive.max")).Value())?.ToString()).IsEqualTo("10");
        await Assert.That((await (await parent.Setting.Get(Storage.InMemory, "archive.max")).Value())?.ToString()).IsEqualTo("50");
    }

    [Test]
    public async Task Clone_Isolates_Writes()
    {
        var ctx = Ctx();
        await ctx.Setting.Set(Storage.InMemory, "archive.max", ctx.Ok(42L));

        var clone = ctx.Setting.Clone();
        await clone.Set(Storage.InMemory, "archive.max", ctx.Ok(999L));

        await Assert.That((await (await clone.Get(Storage.InMemory, "archive.max")).Value())?.ToString()).IsEqualTo("999");
        await Assert.That((await (await ctx.Setting.Get(Storage.InMemory, "archive.max")).Value())?.ToString()).IsEqualTo("42");
    }
}
