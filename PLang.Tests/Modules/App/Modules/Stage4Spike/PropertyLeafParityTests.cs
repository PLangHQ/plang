using ActionEl = global::app.goal.step.action.@this;

namespace PLang.Tests.App.Modules.Stage4Spike;

// action.Return parity — the leaf's return entity must agree with Describe()'s Return on
// polymorphic-vs-concrete. (Per-param desc parity moved to CatalogTests/ParamDescParityTests, the
// 4d enforced gate: full desc reconstruction + the host-drop / text-binary named-exception list.)
public class PropertyLeafParityTests
{
    [Test]
    public async Task ActionReturn_MatchesDescribe_PolymorphicVsConcrete()
    {
        var app = global::PLang.Tests.TestApp.Create("/tmp/s4-return");
        var catalog = await app.Module.Describe();
        var ctx = app.User.Context;

        var mismatches = new System.Collections.Generic.List<string>();
        foreach (var described in catalog)
        {
            var element = new ActionEl
            {
                Module = described.Module, Name = described.Name,
            };
            // Describe and the element now read the SAME member off the same handler, so this
            // asserts the catalog walk and a freshly-built element agree — not two rival sources.
            if (!string.Equals(described.Return, element.Return, System.StringComparison.Ordinal))
                mismatches.Add($"{described.Module}.{described.Name}: element '{element.Return}' vs describe '{described.Return}'");
        }
        await Assert.That(mismatches).IsEmpty()
            .Because("action.Return polymorphic/concrete must agree with Describe: " + string.Join(" | ", mismatches.Take(20)));
    }
}
