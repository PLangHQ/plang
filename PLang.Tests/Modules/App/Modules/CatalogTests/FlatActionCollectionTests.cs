using Action = global::app.goal.step.action.@this;

namespace PLang.Tests.App.Modules.CatalogTests;

/// <summary>
/// The flat action collection at <c>app.module.action</c> enumerates every module's actions AND
/// modifiers at <c>.list</c> — the cross-module catalog surface the builder walks. Proven both as
/// the C# door and through the real <c>%!app.module.action.list%</c> variable navigation (the path
/// the dissolved <c>build.actions</c> reaches the catalog by).
/// </summary>
public class FlatActionCollectionTests
{
    private static IEnumerable<string> Names(global::app.type.item.list.@this list)
        => list.Items.Select(d => d.Peek()?.Clr<Action>()?.Name).Where(n => n != null)!;

    [Test]
    public async Task Action_List_FlattensEveryModulesActions()
    {
        await using var app = TestApp.Create("/app");
        var names = Names(app.Module.action.list).ToHashSet();

        await Assert.That(names).Contains("file.read");
        await Assert.That(names).Contains("variable.set");
        // a modifier rides the same flat surface (structural role, same catalog)
        await Assert.That(names).Contains("cache.wrap");
    }

    [Test]
    public async Task Action_List_ResolvesThrough_BangAppNavigation()
    {
        await using var app = TestApp.Create("/app");
        var ctx = app.User.Context;

        // The exact navigation the builder goal uses once build.actions dissolves.
        var resolved = await (await ctx.Variable.Get("!app.module.action.list")).Value();
        await Assert.That(resolved).IsTypeOf<global::app.type.item.list.@this>();

        var names = Names((global::app.type.item.list.@this)resolved!).ToHashSet();
        await Assert.That(names).Contains("file.read");
        await Assert.That(names).Contains("cache.wrap");
    }
}
