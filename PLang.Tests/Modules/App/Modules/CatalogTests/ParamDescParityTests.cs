using Property = global::app.goal.step.action.property.@this;

namespace PLang.Tests.App.Modules.CatalogTests;

/// <summary>
/// The 4d parity gate (enforced half): for every catalog action+param, the desc the NEW property
/// row composes equals the string <c>Describe()</c> emits today — read in C#, never through Fluid.
/// This proves the template's param loop reconstructs the desc text from row facts (Type face +
/// <c>?</c> if Nullable + <c>= x</c> if Default + <c>%var%</c> if IsVariable) without resurrecting
/// the removed clr&lt;StepActions&gt; render.
///
/// <para>Every intentional delta is LINE-ITEMED below — nothing waved through in bulk:
/// <list type="bullet">
/// <item><b>Host params dropped</b> — a param whose type is a host (<c>clr</c>) is hidden from the
///   catalog (the LLM can't author a host object; naming it would leak the C# type). Present in
///   Describe, absent in the new catalog.</item>
/// <item><b>text/binary win</b> — two params carry the true plang scalar name; Describe's
///   <c>string</c>/<c>bytes</c> are the retired names.</item>
/// </list></para>
/// </summary>
public class ParamDescParityTests
{
    // Host params intentionally HIDDEN — the LLM never supplies a host object (Goal/Step/SignOptions/
    // BuildResponse/StepActions/App). Keyed "module.action.Param". (goal.getTypes dies at 4e.)
    private static readonly HashSet<string> HostDropped = new(StringComparer.Ordinal)
    {
        "goal.getTypes.Goal",
        "http.request.SignOptions", "http.download.SignOptions", "http.upload.SignOptions",
        "environment.run.Step", "environment.run.Action",
        "error.handle.Action",
        "build.validate.Action",
        "build.goalsSave.Goal", "build.goalsSave.App",
        "build.enrichResponse.StepResults", "build.enrichResponse.Goal",
        "build.validateStepActions.Step",
        "build.validateResponse.StepResults", "build.validateResponse.Goal",
        "build.merge.Step", "build.merge.StepFromLlm",
    };

    // Accepted plang-name improvements — the entity faces the true name; Describe's is retired.
    private static readonly Dictionary<string, (string Old, string New)> Renamed = new(StringComparer.Ordinal)
    {
        ["output.write.channel"] = ("string?", "text?"),
        ["signing.sign.RawBytes"] = ("bytes", "binary"),
    };

    // FormatDefault, mirrored from Describe (list/this.cs) — the composition the template must match.
    private static string FmtDefault(object? v) => v switch
    {
        null => "null",
        string s => $"\"{s}\"",
        bool b => b ? "true" : "false",
        _ => v.ToString() ?? "null",
    };

    private static string RowDesc(Property row)
    {
        if (row.IsVariable) return "%var%";
        var t = row.Type.ToString();
        if (row.Nullable && !t.EndsWith("?")) t += "?";
        if (row.Default != null) t += $" = {FmtDefault(row.Default)}";
        return t;
    }

    [Test]
    public async Task ParamDesc_Parity_OnlyNamedExceptions()
    {
        await using var app = TestApp.Create("/app");
        var catalog = await app.Module.Describe();

        var seenDropped = new HashSet<string>(StringComparer.Ordinal);
        var seenRenamed = new HashSet<string>(StringComparer.Ordinal);
        var unexplained = new List<string>();

        foreach (var da in catalog)
        {
            var element = app.Module[da.Module]?[da.ActionName];
            var rows = element == null
                ? new Dictionary<string, Property>()
                : element.Properties.Items
                    .Select(d => d.Peek()?.Clr<Property>())
                    .Where(r => r != null).Select(r => r!)
                    .ToDictionary(r => r.Name, StringComparer.OrdinalIgnoreCase);

            foreach (var p in da.Parameter ?? new())
            {
                var key = $"{da.Module}.{da.ActionName}.{p.Name}";
                var oldDesc = p.Peek()?.ToString() ?? "";

                if (!rows.TryGetValue(p.Name, out var row))
                {
                    // No row = the param was filtered. Legal ONLY for a named host drop.
                    if (HostDropped.Contains(key)) seenDropped.Add(key);
                    else unexplained.Add($"{key}: OLD='{oldDesc}' dropped but NOT a named host param");
                    continue;
                }

                var newDesc = RowDesc(row);
                if (string.Equals(oldDesc, newDesc, StringComparison.Ordinal)) continue;

                if (Renamed.TryGetValue(key, out var rn) && rn.Old == oldDesc && rn.New == newDesc)
                    seenRenamed.Add(key);
                else
                    unexplained.Add($"{key}: OLD='{oldDesc}' NEW='{newDesc}' — unexplained drift");
            }
        }

        // 1. No drift outside the named-exception list.
        await Assert.That(unexplained).IsEmpty();
        // 2. The exception lists are not stale — every named entry was actually exercised.
        await Assert.That(HostDropped.Except(seenDropped).ToList()).IsEmpty();
        await Assert.That(Renamed.Keys.Except(seenRenamed).ToList()).IsEmpty();
    }
}
