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
        return (new PLangEngine(root), root);
    }

    [Test] public async Task GoalPath_Property_IsPathTyped_NotString()
    {
        var prop = typeof(Goal).GetProperty("Path");
        await Assert.That(prop).IsNotNull();
        await Assert.That(prop!.PropertyType).IsEqualTo(typeof(global::app.type.path.@this));
    }

    [Test] public async Task GoalPrPath_IsDerivedFromPath_ViaInBuildFolder()
    {
        var (app, _) = MakeApp();
        var context = app.User.Context;
        var goal = new Goal
        {
            Name = "Test",
            Path = global::app.type.path.@this.Resolve("/Cache/Start.goal", context)
        };
        await Assert.That(goal.PrPath).IsNotNull();
        var rel = goal.PrPath!.Relative.Replace('\\', '/');
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
            Path = global::app.type.path.@this.Resolve("/Start.goal", context),
            PrPath = global::app.type.path.@this.Resolve("/SomeOther/junk.pr", context)
        };
        var rel = goal.PrPath!.Relative.Replace('\\', '/');
        // Init was a no-op — derived from Path, not the explicitly-passed PrPath.
        await Assert.That(rel).IsEqualTo("/.build/start.pr");
    }

    [Test] public async Task GoalGetRuntimeDirectory_DerivesFromLoadedFromPrPath()
    {
        var (app, root) = MakeApp();
        var context = app.User.Context;
        var goal = new Goal { Name = "Test" };
        goal.LoadedFromPrPath = global::app.type.path.@this.Resolve("/Cache/.build/test.pr", context);
        goal.App = app;
        var dir = goal.GetRuntimeDirectory();
        await Assert.That(dir).IsNotNull();
        await Assert.That(dir!.Relative.Replace('\\', '/').TrimStart('/').TrimStart('.').TrimStart('/'))
            .Contains("Cache");
    }

    [Test] public async Task Goal_JsonRoundTrip_PreservesPathAsRelativeString()
    {
        var (app, _) = MakeApp();
        var context = app.User.Context;
        var goal = new Goal
        {
            Name = "Test",
            Path = global::app.type.path.@this.Resolve("/Cache/Start.goal", context)
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
            Path = global::app.type.path.@this.Resolve("/Start.goal", ctx1)
        };
        var opts1 = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Converters = { new global::app.channel.serializer.json.Converter(ctx1) }
        };
        var json = JsonSerializer.Serialize(goal, opts1);
        // Load under a different App / Context.
        var (app2, _) = MakeApp();
        var ctx2 = app2.User.Context;
        var opts2 = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            Converters = { new global::app.channel.serializer.json.Converter(ctx2) }
        };
        var loaded = JsonSerializer.Deserialize<Goal>(json, opts2);
        await Assert.That(loaded).IsNotNull();
        await Assert.That(loaded!.Path).IsNotNull();
        await Assert.That(loaded.Path!.Context).IsEqualTo(ctx2);
    }

    [Test] public async Task Goal_JsonRoundTrip_BackReferencePass_WiresPathContext()
    {
        var (app, _) = MakeApp();
        var context = app.User.Context;
        var goal = new Goal
        {
            Name = "Test",
            Path = global::app.type.path.@this.Resolve("/Start.goal", context)
        };
        var opts = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
            Converters = { new global::app.channel.serializer.json.Converter(context) }
        };
        var json = JsonSerializer.Serialize(goal, opts);
        var loaded = JsonSerializer.Deserialize<Goal>(json, opts);
        await Assert.That(loaded!.Path!.Context).IsEqualTo(context);
    }

    [Test] public async Task GoalCallPrPath_RoundTrips_AsRelativeString()
    {
        var (app, _) = MakeApp();
        var context = app.User.Context;
        var gc = new GoalCall
        {
            Name = "Foo",
            PrPath = global::app.type.path.@this.Resolve("/Cache/.build/foo.pr", context)
        };
        var opts = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Converters = { new global::app.channel.serializer.json.Converter(context) }
        };
        var json = JsonSerializer.Serialize(gc, opts);
        await Assert.That(json).Contains("Cache/.build/foo.pr");
        var loaded = JsonSerializer.Deserialize<GoalCall>(json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true,
                Converters = { new global::app.channel.serializer.json.Converter(context) } });
        await Assert.That(loaded!.PrPath).IsNotNull();
    }

    [Test] public async Task PathJsonConverter_LeavesContextNullAtDeserializeTime()
    {
        // The context-less converter produces a stub Path with Context=null.
        var opts = new JsonSerializerOptions
        {
            Converters = { new global::app.channel.serializer.json.Converter() }
        };
        var p = JsonSerializer.Deserialize<global::app.type.path.@this>("\"/Start.goal\"", opts);
        await Assert.That(p).IsNotNull();
        await Assert.That(p!.Context).IsNull();
    }
}
