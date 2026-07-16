using ActionEl = global::app.goal.steps.step.actions.action.@this;
using PropRow = global::app.goal.steps.step.actions.action.property.@this;

namespace PLang.Tests.App.Modules.Stage4Spike;

// 4c.1 parity gate — the reflection leaf (action.Properties) must reproduce Describe()'s per-param
// output for EVERY action in the catalog, because 4d's template renders from the rows and its gate
// diffs against today's Describe()-driven prompt. A row → its reconstructed "desc" string must equal
// Describe()'s param.Value for the same param.
public class PropertyLeafParityTests
{
    private static string FormatDefault(object? value) => value switch
    {
        null => "null",
        string s => $"\"{s}\"",
        bool b => b ? "true" : "false",
        _ => value.ToString() ?? "null",
    };

    // Reconstruct the param string the OLD Describe() produced, from a NEW property row.
    private static string DescOf(PropRow r)
    {
        var desc = r.IsVariable ? "%var%" : r.Type.ToString() + (r.Nullable && !r.Type.ToString().EndsWith("?") ? "?" : "");
        // Describe appends " = <default>" only when the [Default] attribute is present. The row
        // carries the value; a genuine [Default(null)] would need a HasDefault flag — flagged if hit.
        if (r.Default != null) desc += $" = {FormatDefault(r.Default)}";
        return desc;
    }

    [Test]
    public async Task ReflectionLeaf_MatchesDescribe_ForEveryParam()
    {
        var app = global::PLang.Tests.TestApp.Create("/tmp/s4-parity");
        var catalog = await app.Module.Describe();
        var ctx = app.User.Context;

        var mismatches = new System.Collections.Generic.List<string>();
        int compared = 0;

        foreach (var described in catalog)
        {
            var element = new ActionEl
            {
                Module = described.Module,
                ActionName = described.ActionName,
                ParameterSchema = described.ParameterSchema,
                Context = ctx,
            };

            var rows = element.Properties.Items
                .Select(d => (PropRow)((global::app.type.clr.@this)d.Peek()!).Value)
                .ToList();
            var described_params = described.Parameters ?? new();

            if (rows.Count != described_params.Count)
            {
                mismatches.Add($"{described.Module}.{described.ActionName}: row count {rows.Count} != describe {described_params.Count}");
                continue;
            }

            for (int i = 0; i < rows.Count; i++)
            {
                var r = rows[i];
                var dp = described_params[i];
                var describeName = dp.Name;
                var describeDesc = (await dp.Value())?.ToString() ?? "";
                var rowDesc = DescOf(r);

                if (!string.Equals(r.Name, describeName, System.StringComparison.Ordinal))
                    mismatches.Add($"{described.Module}.{described.ActionName} #{i}: name '{r.Name}' != '{describeName}'");
                else if (!string.Equals(rowDesc, describeDesc, System.StringComparison.Ordinal))
                    mismatches.Add($"{described.Module}.{described.ActionName}.{r.Name}: '{rowDesc}' != '{describeDesc}'");
                compared++;
            }
        }

        await Assert.That(compared).IsGreaterThan(50);   // exercised a real catalog
        await Assert.That(mismatches).IsEmpty()
            .Because("reflection-leaf rows must reproduce Describe(): " + string.Join(" | ", mismatches.Take(25)));
    }
}
