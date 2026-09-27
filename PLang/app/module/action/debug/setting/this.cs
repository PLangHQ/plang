namespace app.module.action.debug.setting;

/// <summary>
/// What <c>--debug</c> shows — <c>%!debug%</c>; <c>--debug={…}</c> is this run's values for it.
/// </summary>
public sealed class @this : global::app.type.item.setting.module.@this
{
    /// <summary>Filter to a specific goal name. Null = all goals.</summary>
    [Out, Store] public global::app.type.item.text.@this? Goal { get; set; }

    /// <summary>Filter to a specific step index. Null = all steps.</summary>
    [Out, Store] public global::app.type.item.number.@this? Step { get; set; }

    /// <summary>The variables to watch, by name. A watched variable prints at every step and logs each time
    /// it is created, changed or deleted. <c>--debug={"variables":["trace","goal"]}</c></summary>
    [Out, Store] public global::app.type.item.list.@this<global::app.type.item.text.@this> Variables { get; set; } = new();

    /// <summary>Max characters per line before truncation. Default 500.</summary>
    [Out, Store] public global::app.type.item.number.@this MaxLength { get; set; } = 500;

    /// <summary>Regex string to filter debug output lines.</summary>
    [Out, Store] public global::app.type.item.text.@this? Grep { get; set; }

    /// <summary>Debug detail level — <see cref="debug.Level.Step"/> (default) or <see cref="debug.Level.Action"/>.</summary>
    [Out, Store] public global::app.type.item.choice.@this<global::app.module.action.debug.Level> Level { get; set; } = global::app.module.action.debug.Level.Step;

    /// <summary>When true, errors include a dump of all available variables at the point of failure.</summary>
    [Out, Store] public global::app.type.item.@bool.@this Verbose { get; set; } = false;

    /// <summary>Granular LLM tracing — each sub-flag dumps one part of the API exchange.
    /// <c>--debug={"llm":{"system":true,"user":true,"response":true,"schema":true}}</c></summary>
    [Out] public global::app.module.action.debug.LlmDebug? Llm { get; set; }
}
