namespace app.goal.list.setting;

/// <summary>
/// What <c>app.goal.list.all(setting)</c> lists: <c>os</c> — the goals of <c>/system/</c> too, beside the
/// app's own; <c>visibility</c> — which goals, a file's public goal and/or the private ones under it.
/// A missing key is its default: every public goal, the system's included.
/// </summary>
public sealed class @this : global::app.type.item.setting.@this
{
    /// <summary>The goals of <c>/system/</c> too, beside the app's own.</summary>
    [Out, Store] public global::app.type.item.@bool.@this Os { get; set; } = true;

    /// <summary>Which goals: public (a file's first), private (the ones under it), or both.</summary>
    [Out, Store] public global::app.type.item.list.@this<global::app.type.item.choice.@this<global::app.goal.Visibility>> Visibility { get; set; }
        = new([(global::app.type.item.choice.@this<global::app.goal.Visibility>)global::app.goal.Visibility.Public]);

    /// <summary>The goals of one file this setting lists: its public goal, the private ones under it,
    /// or both.</summary>
    internal IEnumerable<global::app.goal.@this> Of(global::app.goal.@this file)
    {
        if (Lists(global::app.goal.Visibility.Public)) yield return file;
        if (Lists(global::app.goal.Visibility.Private))
            foreach (var sub in file.Child.Items()) yield return sub;
    }

    // Each row compared by what it names — a row given as text (the call's "private") names it as a
    // choice does; a re-tagged list converts its rows only when they are taken out.
    private bool Lists(global::app.goal.Visibility visibility)
        => Visibility.Slots().Any(v => string.Equals(v?.ToString(), visibility.ToString(), System.StringComparison.OrdinalIgnoreCase));
}
