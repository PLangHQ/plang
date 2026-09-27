namespace app.test.setting;

/// <summary>
/// How a test run runs — <c>%!app.test.setting%</c>; <c>--test={…}</c> is this run's values for it. Bounds are
/// sentinels, not errors: a run (<c>app.test.list.Start</c>) reads a timeout ≤ 0 as none and a parallelism ≤ 0 as one per
/// processor.
/// </summary>
public sealed class @this : global::app.type.item.setting.@this
{
    /// <summary>The actor the run's session opens on — the user, unless it says system.</summary>
    [Out, Store] public global::app.type.item.choice.@this<global::app.actor.Name> Actor { get; set; } = global::app.actor.Name.user;

    /// <summary>Per-test wall-clock timeout in seconds. Default 30.</summary>
    [Out, Store] public global::app.type.item.number.@this TimeoutSeconds { get; set; } = 30;

    /// <summary>How many tests run at once. Default one per processor.</summary>
    [Out, Store] public global::app.type.item.number.@this Parallel { get; set; } = System.Environment.ProcessorCount;

    /// <summary>The report's file format. The console is always written.</summary>
    [Out, Store] public global::app.type.item.choice.@this<global::app.test.Format> Format { get; set; } = global::app.test.Format.Json;

    /// <summary>The tags a test must carry one of to run (empty = every test). Case-insensitive.</summary>
    [Out, Store] public global::app.type.item.list.@this<global::app.type.item.text.@this> Include { get; set; } = new();

    /// <summary>The tags that leave a test out (empty = none). Applied after include — exclude wins.</summary>
    [Out, Store] public global::app.type.item.list.@this<global::app.type.item.text.@this> Exclude { get; set; } = new();
}
