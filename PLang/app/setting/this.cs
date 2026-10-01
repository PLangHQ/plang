namespace app.setting;

/// <summary>
/// The app's own settings — <c>%!app.setting%</c>; <c>--app={…}</c> is this run's values for them. Its name and
/// environment are settings, layered like any (the asker's, falling back to the system's); its id is the app's
/// identity, read here but never set.
/// </summary>
public sealed class @this : global::app.type.item.setting.@this
{
    /// <summary>Allow creating a new app where none exists. <c>--app={"create":true}</c>.</summary>
    [Out, Store] public global::app.type.item.@bool.@this Create { get; set; } = global::app.type.item.@bool.@this.False;

    /// <summary>The app's name. Unset, the app goes by its folder's name.</summary>
    [Out, Store] public global::app.type.item.text.@this? Name { get; set; }

    /// <summary>The environment the app runs in ("production", "development").</summary>
    [Out, Store] public global::app.type.item.text.@this Environment { get; set; } = "production";

    /// <summary>How numbers read as text (<c>is-IS</c>: <c>7,47</c>) — the machine's when unset. Json and the
    /// wire never use it.</summary>
    [Out, Store] public global::app.type.item.culture.@this Culture { get; set; } = global::app.type.item.culture.@this.Machine;

    /// <summary>Past its options, the app's id — its identity's, kept in <c>app.pr</c>: the store needs it before
    /// any setting row can be read, so it is read here and never set.</summary>
    protected override System.Threading.Tasks.ValueTask<global::app.data.@this> Next(global::app.data.@this parent, string key)
        => string.Equals(key, "id", System.StringComparison.OrdinalIgnoreCase)
            ? System.Threading.Tasks.ValueTask.FromResult(new global::app.data.@this(key, parent.Context.App.Id, parent: parent))
            : base.Next(parent, key);
}
