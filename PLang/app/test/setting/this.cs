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

    /// <summary>How many tests run at once; 0 (the default) is one per processor — a default the same on every
    /// machine, so it reads (and is taught) the same everywhere.</summary>
    [Out, Store] public global::app.type.item.number.@this Parallel { get; set; } = 0;

    /// <summary>The report's file format. The console is always written.</summary>
    [Out, Store] public global::app.type.item.choice.@this<global::app.test.Format> Format { get; set; } = global::app.test.Format.Json;

    /// <summary>The tags a test must carry one of to run (empty = every test). Case-insensitive.</summary>
    [Out, Store] public global::app.type.item.list.@this<global::app.type.item.text.@this> Include { get; set; } = new();

    /// <summary>The tags that leave a test out (empty = none). Applied after include — exclude wins.</summary>
    [Out, Store] public global::app.type.item.list.@this<global::app.type.item.text.@this> Exclude { get; set; } = new();

    /// <summary>Why these options leave <paramref name="test"/> out — a tag in <see cref="Exclude"/> (exclude
    /// wins), or no tag in a non-empty <see cref="Include"/>. Null when the run takes it. Tags compare by the
    /// tag's own equality.</summary>
    public global::app.type.item.text.@this? Exclusion(global::app.test.@this test, global::app.actor.context.@this context)
    {
        var tags = test.Tags.Items().ToHashSet();
        // Each filter row is taken out as a value (a list set from the CLI holds its raw rows).
        bool Carries(global::app.type.item.list.@this<global::app.type.item.text.@this> filter)
            => filter.Items(context).Any(row => global::app.type.item.tag.@this.Create(row.Peek()) is { } tag && tags.Contains(tag));

        if (Exclude.CountRaw > 0 && Carries(Exclude)) return "excluded by tag";
        if (Include.CountRaw > 0 && !Carries(Include)) return "no include match";
        return null;
    }
}
