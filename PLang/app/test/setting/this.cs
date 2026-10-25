namespace app.test.setting;

/// <summary>
/// How a test run runs — <c>%!app.test.setting%</c>; <c>--test.setting.…</c> is this run's values for it (and alone switches test on). Bounds are
/// sentinels, not errors: a run (<c>app.test.list.Start</c>) reads a timeout ≤ 0 as none and a parallelism ≤ 0 as one per
/// processor.
/// </summary>
public sealed class @this : global::app.type.item.setting.@this
{
    /// <summary>Born holding its type — the app's list hands it its own class.</summary>
    public @this(global::app.type.@this type) : base(type) { }

    /// <summary>A settings class is a type but never LLM vocabulary — kept out of the catalog.</summary>
    public static bool Internal => true;

    /// <summary>The actor the run's session opens on — the user, unless it says system.</summary>
    [Out, Store] public global::app.type.item.choice.@this<global::app.actor.Name> Actor { get; set; } = global::app.actor.Name.user;

    /// <summary>Per-test wall-clock timeout. Default 30s.</summary>
    [Out, Store] public global::app.type.item.duration.@this Timeout { get; set; } = System.TimeSpan.FromSeconds(30);

    /// <summary>Where a test's llm questions are answered: <c>saved</c> (the default), from the file beside the test
    /// (<c>X.test.llm.json</c>), a question not in it asked once and added, and the file kept to what a passing run
    /// asked; or <c>live</c>, every one asked of the llm, the file neither read nor written.</summary>
    [Out, Store] public global::app.type.item.choice.@this<global::app.test.llm> Llm { get; set; } = global::app.test.llm.saved;

    /// <summary>How many tests run at once; 0 (the default) is one per processor — a default the same on every
    /// machine, so it reads (and is taught) the same everywhere.</summary>
    [Out, Store] public global::app.type.item.number.@this Parallel { get; set; } = 0;

    /// <summary>How many tests run at once on this machine: <see cref="Parallel"/>, or one per processor for 0.</summary>
    internal int Width => Parallel.ToInt32() is var parallel and > 0 ? parallel : System.Environment.ProcessorCount;

    /// <summary>The report's format — a format kind that writes one (json, junit). The console is always written.</summary>
    [Out, Store] public global::app.type.item.choice.@this<global::app.test.format.@this> Format { get; set; } = new global::app.test.format.@this("json");

    /// <summary>The tags a test must carry one of to run (empty = every test). Case-insensitive.</summary>
    [Out, Store] public global::app.type.item.list.@this<global::app.type.item.text.@this> Include { get; set; }

    /// <summary>The tags that leave a test out (empty = none). Applied after include — exclude wins.</summary>
    [Out, Store] public global::app.type.item.list.@this<global::app.type.item.text.@this> Exclude { get; set; }

    /// <summary>Why these options leave <paramref name="test"/> out — a tag in <see cref="Exclude"/> (exclude
    /// wins), or no tag in a non-empty <see cref="Include"/>. Null when the run takes it. Tags compare by the
    /// tag's own equality.</summary>
    public global::app.type.item.text.@this? Exclusion(global::app.test.@this test, global::app.actor.context.@this context)
    {
        var tags = test.Tags.Items().ToHashSet();
        var type = context.App.type.list[typeof(global::app.type.item.tag.@this)];
        // Each filter row is taken out as a value (a list set from the CLI holds its raw rows).
        bool Carries(global::app.type.item.list.@this<global::app.type.item.text.@this> filter)
            => filter.Items(context).Any(row => global::app.type.item.tag.@this.Create(row.Peek(), type) is { } tag && tags.Contains(tag));

        if (Exclude.CountRaw > 0 && Carries(Exclude)) return "excluded by tag";
        if (Include.CountRaw > 0 && !Carries(Include)) return "no include match";
        return null;
    }
}
