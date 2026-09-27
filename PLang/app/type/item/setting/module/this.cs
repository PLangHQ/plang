namespace app.type.item.setting.module;

/// <summary>
/// A module's settings — <c>%!llm%</c>, <c>%!http%</c>: its own options (a module with a setting class of
/// its own derives this, <c>module/action/llm/setting</c>) and its actions by name (<c>%!llm.query%</c>).
/// </summary>
public class @this : global::app.type.item.setting.@this
{
    /// <summary>A module with no setting class of its own.</summary>
    public @this(string module) : base(module) { }

    /// <summary>A module's own class — its path is the module's name.</summary>
    protected @this() { }

    /// <summary>An action of this module, as its settings (<c>%!llm.query%</c>).</summary>
    protected override async System.Threading.Tasks.ValueTask<global::app.data.@this> Next(global::app.data.@this parent, string key)
    {
        var module = await parent.Context.App.module.Get(Path);
        if (!module.Success) return module;
        return (await module.Value())![key] is { } action
            ? new global::app.data.@this(key, new global::app.type.item.setting.action.@this(action), parent: parent)
            : parent.Context.NotFound(key);
    }

    /// <summary>This run's value for an option of the module: one of its own class's, else one any of its
    /// actions takes (<c>set %!http.timeout% = 5</c> → <c>http.timeout</c>, which each of those actions
    /// reads after its own key).</summary>
    public override async System.Threading.Tasks.ValueTask<global::app.type.item.@this> Set(string key, bool isIndex,
        object? value, global::app.actor.context.@this context)
    {
        if (Option(key) != null) return await base.Set(key, isIndex, value, context);
        var module = await context.App.module.Get(Path);
        if (!module.Success || await module.Value() is not { } found
            || !found.ActionNames.Any(name => found[name]?.Property[key] != null))
            throw new System.NotSupportedException($"module '{Path}' has no option '{key}'");
        await Write(key, value, context);
        return this;
    }
}
