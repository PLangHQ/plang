using System.Text.Json;
using System.Text.Json.Nodes;

namespace PLang.Tests.App.Types;

// plang-types — Stage 1/3 build-time kind stamping.
// The builder stamps `kind` on a .pr parameter ONLY when the action's declared
// parameter type carries a static Build(value) hook. Polymorphic Data<object>
// slots (e.g. variable.set's Value) get type:object with NO kind — by design.
//
// Asserts shape of pre-built .pr files committed under Tests/ — exercising the
// real build pipeline output without re-invoking the builder from C#.

public class BuilderKindStampingTests
{
    private static readonly string TestsRoot = System.IO.Path.GetFullPath(
        System.IO.Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "Tests"));

    private static JsonNode LoadPr(string relativePath)
    {
        var full = System.IO.Path.Combine(TestsRoot, relativePath);
        var json = System.IO.File.ReadAllText(full);
        return JsonNode.Parse(json)!;
    }

    private static JsonNode FindParam(JsonNode pr, int stepIndex, int actionIndex, string paramName)
    {
        var param = pr["steps"]![stepIndex]!["actions"]![actionIndex]!["parameters"]!.AsArray()
            .First(p => string.Equals(p!["name"]?.GetValue<string>(), paramName, System.StringComparison.OrdinalIgnoreCase));
        return param!;
    }

    [Test] public async Task TypedKindBearingParam_StampsKind_OnPathParameter()
    {
        // file.read's Path parameter is typed `path`; path has a Build hook that
        // returns the scheme ("file" for a bare relative path).
        var pr = LoadPr("Types/.build/readphotostampsimage.test.pr");
        var pathParam = FindParam(pr, 0, 0, "Path");
        await Assert.That(pathParam["type"]?.GetValue<string>()).IsEqualTo("path");
        await Assert.That(pathParam["kind"]?.GetValue<string>()).IsEqualTo("file");
    }

    [Test] public async Task PolymorphicValueSlot_HasNoKind_TypeIsObject()
    {
        // variable.set's Value is Data<object> (polymorphic). No kind stamp —
        // the builder skips kind-of for object-typed slots so the .pr stays
        // honest about what the build pipeline knew vs. what the runtime decides.
        var pr = LoadPr("Types/.build/setdecimalliteralstampskind.test.pr");
        var valueParam = FindParam(pr, 0, 0, "Value");
        await Assert.That(valueParam["type"]?.GetValue<string>()).IsEqualTo("object");
        await Assert.That(valueParam["kind"]).IsNull();
    }

    [Test] public async Task MathAddResult_HasNoKind_PolymorphicAtBuild()
    {
        // The math.add forwarder owns construction of its Data, but the result
        // slot is polymorphic at build — no kind in the .pr. The variable.set
        // that writes %b% has Value=%!data% (a runtime reference) with no kind.
        var pr = LoadPr(".build/cut1_literalkindarithmeticoutput.test.pr");
        // Step 3 = math.add A=%x% B=%z%, write to %b%
        // Locate the variable.set action that captures the result.
        var steps = pr["steps"]!.AsArray();
        var setAction = steps.SelectMany(s => s!["actions"]!.AsArray())
            .First(a => a!["module"]?.GetValue<string>() == "variable"
                     && a["action"]?.GetValue<string>() == "set"
                     && a["parameters"]!.AsArray().Any(p => p!["name"]?.GetValue<string>() == "Name"
                                                            && p["value"]?.GetValue<string>() == "%b%"));
        var valueParam = setAction!["parameters"]!.AsArray()
            .First(p => string.Equals(p!["name"]?.GetValue<string>(), "Value", System.StringComparison.OrdinalIgnoreCase));
        await Assert.That(valueParam!["kind"]).IsNull();
    }
}
