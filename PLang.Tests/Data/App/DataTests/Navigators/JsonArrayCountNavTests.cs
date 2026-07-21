using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;

namespace PLang.Tests.App.DataTests.Navigators;

/// <summary>
/// A clr(json) array navigates the synthetic collection keys like a plang list: .count/.length/.size
/// (size for Fluid), .first/.last. Regression for the builder's <c>%plan.steps.Count%</c> — the LLM
/// planner result is a clr(json), and the json kind previously only did property + numeric index, so
/// <c>.Count</c> on an array was unreachable.
/// </summary>
public class JsonArrayCountNavTests
{
    private static async Task<string?> Nav(app.@this app, string path)
    {
        var ctx = app.User.Context;
        var plan = await ctx.Ok("{\"steps\":[10,20,30]}", ctx.App.Type.Kind["json"]);
        plan.Name = "plan";
        var hit = await plan.Get(path);
        return (await hit.Value())?.ToString();
    }

    [Test] public async Task JsonArray_Count()  { await using var app = global::PLang.Tests.TestApp.Create("/test"); await Assert.That(await Nav(app, "steps.Count")).IsEqualTo("3"); }
    [Test] public async Task JsonArray_Length() { await using var app = global::PLang.Tests.TestApp.Create("/test"); await Assert.That(await Nav(app, "steps.length")).IsEqualTo("3"); }
    [Test] public async Task JsonArray_Size()   { await using var app = global::PLang.Tests.TestApp.Create("/test"); await Assert.That(await Nav(app, "steps.size")).IsEqualTo("3"); }
    [Test] public async Task JsonArray_First()  { await using var app = global::PLang.Tests.TestApp.Create("/test"); await Assert.That(await Nav(app, "steps.first")).IsEqualTo("10"); }
    [Test] public async Task JsonArray_Last()   { await using var app = global::PLang.Tests.TestApp.Create("/test"); await Assert.That(await Nav(app, "steps.last")).IsEqualTo("30"); }
}
