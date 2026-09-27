using System.Text.Json.Serialization;

namespace app.goal.step;

// The step at the build's walk over a scratch store (goal.step.list.Scope).
public sealed partial class @this
{
    private global::app.type.property.list.@this _typed = new();
    /// <summary>The variables the step's words name that the build knows the type of where the step
    /// reads them — known before one of its actions runs (a variable the step only writes is not read
    /// by it). Left by the walk; empty until then. Build-time only.</summary>
    [JsonIgnore]
    public global::app.type.property.list.@this Typed => _typed;

    private List<global::app.type.item.setting.kind.@this> _setting = [];
    /// <summary>The classes of settings the step's words name (<c>%!llm.cache%</c> → llm's,
    /// <c>%!app.test.setting.parallel%</c> → test's), in the order written — what the builder teaches the
    /// step's options from. Left by the walk; empty until then. Build-time only.</summary>
    [JsonIgnore]
    public IReadOnlyList<global::app.type.item.setting.kind.@this> Setting => _setting;

    /// <summary>This step at the build's walk: its code — or, while it has none, the code its certain
    /// picks already know (<c>Pick.Known</c>) — walked action by action over <paramref name="scratch"/>,
    /// leaving <see cref="Typed"/> and <see cref="Setting"/>. One error per property the store says is the
    /// wrong type.</summary>
    public async Task<List<global::app.error.Error>> Scope(global::app.actor.context.@this scratch)
    {
        var variables = new global::app.type.item.variable.parser.@this(Text).Variable;
        // each %!…% names the class of settings its longest path is
        var classes = scratch.App.type.list["setting"].kind;
        _setting = variables
            .Select(v => v.Paths.Select(path => classes[path]).OfType<global::app.type.item.setting.kind.@this>().LastOrDefault())
            .OfType<global::app.type.item.setting.kind.@this>().Distinct().ToList();
        // the bare names the step's words write: %name% — not a setting (%!x%), not a way in (%x.y%)
        var names = variables
            .Where(v => v.IsBare).Select(v => v.Code.Root.Name).Distinct().ToList();
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
        _typed = new();
        foreach (var name in names.Where(known.ContainsKey))
            _typed.Add(new global::app.type.property.@this { Name = name, Type = known[name] });
        return declined;
    }
}
