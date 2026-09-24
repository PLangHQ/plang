using System.Text.Json;
using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using PLangEngine = global::app.@this;

namespace PLang.Tests.App.Goals.PathTypingTests;

/// <summary>
/// Batch 4. Goal/GoalCall typing flip.
/// </summary>
public class GoalPathTypingTests
{
    private static (PLangEngine app, string root) MakeApp()
    {
        var root = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "plang-goalpathtyping-" + System.Guid.NewGuid().ToString("N"));
        System.IO.Directory.CreateDirectory(root);
        return (TestApp.Create(root), root);
    }

    [Test] public async Task GoalPath_Property_IsPathTyped_NotString()
    {
        var prop = typeof(Goal).GetProperty("Path");
        await Assert.That(prop).IsNotNull();
        await Assert.That(prop!.PropertyType).IsEqualTo(typeof(global::app.type.item.path.@this));
    }

    [Test] public async Task GoalPrPath_IsDerivedFromPath_ViaInBuildFolder()
    {
        var (app, _) = MakeApp();
        var context = app.User.Context;
        var goal = new Goal
        {
            Name = "Test",
            Path = global::app.type.item.path.@this.Resolve("/Cache/Start.goal", context)
        };
        await Assert.That(goal.PrPath).IsNotNull();
        var rel = goal.PrPath!.Relative(context).Replace('\\', '/');
        await Assert.That(rel).IsEqualTo("/Cache/.build/start.pr");
    }

    [Test] public async Task GoalPrPath_InitSetter_IsNoOp_SwallowsJsonValue()
    {
        var (app, _) = MakeApp();
        var context = app.User.Context;
        // Construct with both Path and a JSON-shaped prPath init — the init {}
        // swallows the value and the getter recomputes from Path.
        var goal = new Goal
        {
            Name = "Test",
            Path = global::app.type.item.path.@this.Resolve("/Start.goal", context),
            PrPath = global::app.type.item.path.@this.Resolve("/SomeOther/junk.pr", context)
        };
        var rel = goal.PrPath!.Relative(context).Replace('\\', '/');
        // Init was a no-op — derived from Path, not the explicitly-passed PrPath.
        await Assert.That(rel).IsEqualTo("/.build/start.pr");
    }

    [Test] public async Task GoalGetRuntimeDirectory_DerivesFromLoadedFromPrPath()
    {
        var (app, root) = MakeApp();
        var context = app.User.Context;
        var goal = new Goal { Name = "Test" };
        goal.LoadedFromPrPath = global::app.type.item.path.@this.Resolve("/Cache/.build/test.pr", context);
        goal.App = app;
        var dir = goal.GetRuntimeDirectory();
        await Assert.That(dir).IsNotNull();
        await Assert.That(dir!.Relative(context).Replace('\\', '/').TrimStart('/').TrimStart('.').TrimStart('/'))
            .Contains("Cache");
    }

    [Test] public async Task Goal_JsonRoundTrip_PreservesPathAsRelativeString()
    {
        var (app, _) = MakeApp();
        var context = app.User.Context;
        var goal = new Goal
        {
            Name = "Test",
            Path = global::app.type.item.path.@this.Resolve("/Cache/Start.goal", context)
        };
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
            Converters = { new global::app.channel.serializer.json.Converter(context) }
        };
        var json = JsonSerializer.Serialize(goal, options);
        await Assert.That(json).Contains("Cache/Start.goal");
    }

    [Test] public async Task Goal_JsonRoundTrip_ReconstitutesPath_UnderDifferentAppRoot()
    {
        var (app1, _) = MakeApp();
        var ctx1 = app1.User.Context;
        var goal = new Goal
        {
            Name = "Test",
            Path = global::app.type.item.path.@this.Resolve("/Start.goal", ctx1)
        };
        // The goal writes its own .pr and is read back under a different App / Context.
        var (app2, _) = MakeApp();
        var ctx2 = app2.User.Context;
        var loaded = await global::PLang.Tests.Shared.RealGoalLoad.ViaChannel(app2, goal);
        await Assert.That(loaded).IsNotNull();
        await Assert.That(loaded!.Path).IsNotNull();
        await Assert.That(loaded.Path!.Relative(ctx2)).IsEqualTo("/Start.goal");
        await Assert.That(loaded.Path!.Absolute).StartsWith(app2.AbsolutePath);
    }

    [Test] public async Task Goal_JsonRoundTrip_ResolvesPathUnderReaderRoot()
    {
        var (app, _) = MakeApp();
        var context = app.User.Context;
        var goal = new Goal
        {
            Name = "Test",
            Path = global::app.type.item.path.@this.Resolve("/Start.goal", context)
        };
        // The goal writes its own .pr and reads itself back.
        var loaded = await global::PLang.Tests.Shared.RealGoalLoad.ViaChannel(app, goal);
        await Assert.That(loaded!.Path!.Relative(context)).IsEqualTo("/Start.goal");
    }

}
