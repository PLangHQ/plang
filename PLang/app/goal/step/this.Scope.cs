using System.Text.Json.Serialization;

namespace app.goal.step;

// The step at the build's walk over a scratch store (goal.step.list.Scope).
public sealed partial class @this
{
    // A variable the step's words name: %name% — not a setting (%!x%), not a navigation (%x.y%).
    private static readonly System.Text.RegularExpressions.Regex Named = new(@"%([A-Za-z_]\w*)%");

    private global::app.type.property.list.@this _variable = new();
    /// <summary>The variables the step's words name that the build knows the type of where the step
    /// reads them — known before one of its actions runs (a variable the step only writes is not read
    /// by it). Left by the walk; empty until then. Build-time only.</summary>
    [JsonIgnore]
    public global::app.type.property.list.@this Variable => _variable;

    /// <summary>This step at the build's walk: its code — or, while it has none, the code its certain
    /// picks already know (<c>Pick.Known</c>) — walked action by action over <paramref name="scratch"/>,
    /// leaving <see cref="Variable"/>. One error per property the store says is the wrong type.</summary>
    public async Task<List<global::app.error.Error>> Scope(global::app.actor.context.@this scratch)
    {
        var names = Named.Matches(Text).Select(m => m.Groups[1].Value).Distinct().ToList();
        var known = new Dictionary<string, global::app.type.@this>();
        var declined = new List<global::app.error.Error>();
        var actions = (Code.Count > 0 ? Code : Pick.Known).Items().ToList();
        // before each action — and once for a step whose code nothing knows yet
        for (int i = 0; i < Math.Max(1, actions.Count); i++)
        {
            foreach (var name in names.Where(n => !known.ContainsKey(n)))
                if (await scratch.Variable.Get(name) is { IsInitialized: true, Type: { } type } && type.Name != "item")
                    known[name] = type;
            if (i < actions.Count) declined.AddRange(await actions[i].Scope(scratch));
        }
        _variable = new();
        foreach (var name in names.Where(known.ContainsKey))
            _variable.Add(new global::app.type.property.@this { Name = name, Type = known[name] });
        return declined;
    }
}
