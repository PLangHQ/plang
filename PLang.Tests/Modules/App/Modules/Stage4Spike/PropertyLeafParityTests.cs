using ActionEl = global::app.goal.step.action.@this;

namespace PLang.Tests.App.Modules.Stage4Spike;

// action.Return parity — the leaf's return entity must agree with Describe()'s ReturnTypeName on
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
                Module = described.Module, ActionName = described.ActionName, Context = ctx,
            };
            // Describe's ReturnTypeName is null (no value) or "data" (polymorphic) or a concrete
            // name; the entity is null for both null/"data"/object, non-null for a concrete type.
            bool describePolymorphic = described.ReturnTypeName is null or "data";
            bool entityPolymorphic = element.Return is null;
            if (describePolymorphic != entityPolymorphic)
                mismatches.Add($"{described.Module}.{described.ActionName}: Return {(entityPolymorphic ? "null" : element.Return!.ToString())} vs ReturnTypeName '{described.ReturnTypeName}'");
        }
        await Assert.That(mismatches).IsEmpty()
            .Because("action.Return polymorphic/concrete must agree with Describe: " + string.Join(" | ", mismatches.Take(20)));
    }
}
