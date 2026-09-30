namespace app.module.build.setting;

/// <summary>
/// The build module's own settings — <c>%!build.setting.cache%</c>, <c>%!build.setting.files%</c>: what the builder reads.
/// <c>--build={…}</c> is this run's values for them.
/// </summary>
public sealed class @this : global::app.type.item.setting.module.@this
{
    /// <summary>Whether the builder's LLM answers are cached. <c>--build={"cache":false}</c> turns it off.</summary>
    [Out, Store] public global::app.type.item.@bool.@this Cache { get; set; } = true;

    /// <summary>The files to build, in order — every goal when empty. A native plang list: each row lifts
    /// to a path at its reader's door (<c>row.Value&lt;path&gt;()</c>).
    /// <c>--build={"files":"test.goal"}</c> or <c>--build={"files":["test.goal","run.goal"]}</c>.</summary>
    [Out, Store] public global::app.type.item.list.@this Files { get; set; } = new();
}
