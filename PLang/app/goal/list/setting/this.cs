namespace app.goal.list.setting;

/// <summary>
/// What <c>app.goal.list.all(setting)</c> lists: <c>os</c> — the goals of <c>/system/</c> too, beside the
/// app's own; <c>visibility</c> — which goals, a file's public goal and/or the private ones under it.
/// A missing key is its default: every public goal, the system's included.
/// </summary>
public sealed class @this
{
    /// <summary>The goals of <c>/system/</c> too, beside the app's own.</summary>
    public global::app.type.item.@bool.@this Os { get; } = true;

    /// <summary>Which goals: public (a file's first), private (the ones under it), or both.</summary>
    public global::app.type.item.list.@this<global::app.type.item.choice.@this<global::app.goal.Visibility>> Visibility { get; }
        = new([(global::app.type.item.choice.@this<global::app.goal.Visibility>)global::app.goal.Visibility.Public]);

    /// <summary>Every default.</summary>
    public @this() { }

    /// <summary>The call's dict read as this setting — each key it gives, the rest their defaults.</summary>
    public @this(global::app.type.item.dict.@this? given)
    {
        if (given == null) return;
        if (given.Has("os")) Os = given.Clr("os", typeof(bool)) is true;
        if (!given.Has("visibility")) return;
        var named = given.Stored("visibility") is global::app.type.item.list.@this many
            ? many.Slots().Select(s => s?.ToString() ?? "")
            : [given.Stored("visibility")?.ToString() ?? ""];
        Visibility = new(named.Select(n => (global::app.type.item.@this)(global::app.type.item.choice.@this<global::app.goal.Visibility>)
            System.Enum.Parse<global::app.goal.Visibility>(n, ignoreCase: true)));
    }

    /// <summary>The goals of one file this setting lists: its public goal, the private ones under it,
    /// or both.</summary>
    internal IEnumerable<global::app.goal.@this> Of(global::app.goal.@this file)
    {
        if (Lists(global::app.goal.Visibility.Public)) yield return file;
        if (Lists(global::app.goal.Visibility.Private))
            foreach (var sub in file.Child.Items()) yield return sub;
    }

    private bool Lists(global::app.goal.Visibility visibility)
        => Visibility.Items().Any(v => Equals(v.Value, visibility));
}
