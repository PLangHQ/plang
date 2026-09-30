namespace app.type.item.setting.module;

/// <summary>
/// A module's settings — <c>%!llm.setting%</c>, <c>%!http.setting%</c>: its own options (a module with a setting
/// class of its own derives this, <c>module/llm/setting</c>), and an option any of its actions takes
/// (<c>%!http.setting.timeout%</c>). An action's own settings are the action's (<c>%!llm.query.setting%</c>).
/// </summary>
public class @this : global::app.type.item.setting.@this
{
    private const string Own = ".setting";

    /// <summary>A module with no setting class of its own.</summary>
    public @this(string module) : base(module + Own) { }

    /// <summary>A module's own class — its path is the module's, then <c>.setting</c>.</summary>
    protected @this() { }

    /// <summary>The module these settings configure — the path before <c>.setting</c>.</summary>
    private string Module => Path[..^Own.Length];

    /// <summary>This run's value for an option of the module: one of its own class's, else one any of its
    /// actions takes (<c>set %!http.setting.timeout% = 5</c> → <c>http.setting.timeout</c>, which each of those
    /// actions reads after its own key).</summary>
    public override async System.Threading.Tasks.ValueTask<global::app.type.item.@this> Set(string key, bool isIndex,
        object? value, global::app.actor.context.@this context)
    {
        if (Option(key) != null) return await base.Set(key, isIndex, value, context);
        var module = await context.App.module.Get(Module);
        if (!module.Success || await module.Value() is not { } found
            || !found.ActionNames.Any(name => found[name]?.Property[key] != null))
            throw new System.NotSupportedException($"module '{Module}' has no option '{key}'");
        await Write(key, value, context);
        return this;
    }
}
