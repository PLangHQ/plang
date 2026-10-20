using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;

namespace PLang.Tests.App.DataTests.Navigators;

/// <summary>
/// A json array, navigated, parses to a plang list (805) and answers the list's own collection keys:
/// .count, .first, .last. Regression for the builder's <c>%plan.steps.Count%</c> — the LLM planner
/// result is json; navigating <c>.Count</c> on its array reaches the parsed list's count.
/// (The .length/.size aliases are retired — the word is .count.)
/// </summary>
public class JsonArrayCountNavTests
{
    private static async Task<string?> Nav(app.@this app, string path)
    {
        var ctx = app.actor.list.User.Context;
        var plan = await ctx.App.type.list["item"].kind["json"]!.Decode(System.Text.Encoding.UTF8.GetBytes("{\"steps\":[10,20,30]}"), ctx);
        plan.Name = "plan";
        var hit = await plan.Get(path);
        return (await hit.Value())?.ToString();
    }

    [Test] public async Task JsonArray_Count()  { await using var app = global::PLang.Tests.Session.App("/test").Testing(); await Assert.That(await Nav(app, "steps.Count")).IsEqualTo("3"); }
    [Test] public async Task JsonArray_First()  { await using var app = global::PLang.Tests.Session.App("/test").Testing(); await Assert.That(await Nav(app, "steps.first")).IsEqualTo("10"); }
    [Test] public async Task JsonArray_Last()   { await using var app = global::PLang.Tests.Session.App("/test").Testing(); await Assert.That(await Nav(app, "steps.last")).IsEqualTo("30"); }
}
