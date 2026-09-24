using PLang.Tests.App.Fixtures;
using app.module.matrix.withdefault;

namespace PLang.Tests.Generator.Matrix.WithDefault;

/// <summary>
/// The binding order: the step's value → a setting → what the build froze (.pr) → the class's
/// [Default]. An explicit setting beats a frozen default; a runtime change of [Default] never does,
/// so a built program runs the same. IntWithDefault declares [Default(42)].
/// </summary>
public class BindingOrderTests
{
    // The setting key the generated binding asks for this fixture: {namespace}.{action}.{property}.
    private const string Key = "app.module.matrix.withdefault.intwithdefault.count";

    private static async Task<long?> Count(global::app.@this app,
        (string, object?)[]? step = null, (string, object?)[]? frozen = null)
    {
        var result = await MatrixRunner.RunAsync<IntWithDefault>(app, parameters: step, defaults: frozen);
        var typed = result.Data as global::app.data.@this<global::app.type.item.number.@this>;
        return (await typed!.Value())?.Clr<long>();
    }

    [Test] public async Task Setting_BeatsAFrozenDefault()
    {
        await using var app = TestApp.Create("/app");
        await app.User.Context.Setting.Set(global::app.setting.Storage.InMemory, Key, app.User.Context.Ok(5L));
        await Assert.That(await Count(app, frozen: new[] { ("count", (object?)30L) })).IsEqualTo(5L);
    }

    [Test] public async Task StepValue_BeatsTheSetting()
    {
        await using var app = TestApp.Create("/app");
        await app.User.Context.Setting.Set(global::app.setting.Storage.InMemory, Key, app.User.Context.Ok(5L));
        await Assert.That(await Count(app, step: new[] { ("count", (object?)7L) })).IsEqualTo(7L);
    }

    [Test] public async Task FrozenDefault_BeatsTheClassDefault()
    {
        await using var app = TestApp.Create("/app");
        await Assert.That(await Count(app, frozen: new[] { ("count", (object?)30L) })).IsEqualTo(30L);
    }

    [Test] public async Task ClassDefault_WhenNothingElseSays()
    {
        await using var app = TestApp.Create("/app");
        await Assert.That(await Count(app)).IsEqualTo(42L);
    }
}
