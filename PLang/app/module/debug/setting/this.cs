namespace app.module.debug.setting;

/// <summary>
/// What <c>--debug</c> shows — <c>%!debug%</c>; <c>--debug.setting.…</c> is this run's values for it (and alone
/// switches debug on).
/// </summary>
public sealed class @this : global::app.type.item.setting.module.@this
{
    /// <summary>Born holding its type — the app's list hands it its own class.</summary>
    public @this(global::app.type.@this type) : base(type) { }

    /// <summary>A settings class is a type but never LLM vocabulary — kept out of the catalog.</summary>
    public static bool Internal => true;

    /// <summary>The goal debug attaches at — it and every goal it calls, until it returns: by its name, the app's goals a
    /// <c>prefix*</c> starts, or by its address (<c>/system/builder/BuildGoal/Decide</c>), which is how one of plang's own
    /// goals is named (they are none of the app's goals). Null or <c>*</c> = every goal of the app.</summary>
    [Out, Store] public global::app.type.item.text.@this? Goal { get; set; }

    /// <summary>The goal <see cref="Goal"/> names by its address — where it lives, without <c>.goal</c>; null when it
    /// names goals by name.</summary>
    internal string? Address => Goal?.ToString() is ['/', ..] address && !address.EndsWith('*') ? address : null;

    /// <summary>Whether <paramref name="goal"/> is one <see cref="Goal"/> names: every goal when it names none (or
    /// <c>*</c>), the goals a <c>prefix*</c> starts, the one named (case aside), or the one at the address named. No goal
    /// (a step composed in C#) is only one when every goal is.</summary>
    internal bool Matches(global::app.goal.@this? goal)
    {
        if (Goal?.ToString() is not { Length: > 0 } pattern || pattern == "*") return true;
        if (goal == null) return false;
        if (Address is { } address)
            return string.Equals(goal.Address, address, System.StringComparison.OrdinalIgnoreCase);
        return pattern.EndsWith('*')
            ? goal.Name.StartsWith(pattern[..^1], System.StringComparison.OrdinalIgnoreCase)
            : string.Equals(goal.Name, pattern, System.StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>The step debug attaches at instead, in each goal named: that step and what it calls. Null = the goal's
    /// whole run.</summary>
    [Out, Store] public global::app.type.item.number.@this? Step { get; set; }

    /// <summary>The variables to watch, by name. A watched variable prints at every step and logs each time
    /// it is created, changed or deleted. <c>--debug.setting.variables=["trace","goal"]</c></summary>
    [Out, Store] public global::app.type.item.list.@this<global::app.type.item.text.@this> Variables { get; set; }

    /// <summary>How long a line runs before it is cut (<c>length.max</c>, default 500).</summary>
    [Out, Store] public length.@this Length { get; set; }

    /// <summary>Regex string to filter debug output lines.</summary>
    [Out, Store] public global::app.type.item.text.@this? Grep { get; set; }

    /// <summary>Debug detail level — <see cref="debug.Level.Step"/> (default) or <see cref="debug.Level.Action"/>.</summary>
    [Out, Store] public global::app.type.item.choice.@this<global::app.module.debug.Level> Level { get; set; } = global::app.module.debug.Level.Step;

    /// <summary>When true, errors include a dump of all available variables at the point of failure.</summary>
    [Out, Store] public global::app.type.item.@bool.@this Verbose { get; set; } = false;

    /// <summary>Granular LLM tracing — each sub-flag dumps one part of the API exchange.
    /// <c>--debug.setting.llm={"system":true,"user":true,"response":true,"schema":true}</c></summary>
    [Out] public llm.@this? Llm { get; set; }
}
