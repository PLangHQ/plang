namespace app.setting;

/// <summary>
/// The app's own settings — <c>%!app.setting%</c>; <c>--app={…}</c> is this run's values for them.
/// </summary>
public sealed class @this : global::app.type.item.setting.@this
{
    /// <summary>Allow creating a new app where none exists. <c>--app={"create":true}</c>.</summary>
    [Out, Store] public global::app.type.item.@bool.@this Create { get; set; } = global::app.type.item.@bool.@this.False;
}
