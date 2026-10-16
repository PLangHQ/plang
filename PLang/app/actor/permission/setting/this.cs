namespace app.actor.permission.setting;

/// <summary>
/// An actor's saved grants — <c>%!app.actor.permission.setting%</c>: each one answered "always" to a
/// permission prompt. An actor's own: a user with no grant never holds the system's. A grant granted for
/// this run only is not saved; it lives on the actor's permission for the run.
/// </summary>
[global::app.type.item.setting.Own]
public sealed class @this : global::app.type.item.setting.@this
{
    /// <summary>A settings class is a type but never LLM vocabulary (774) — kept out of the catalog.</summary>
    public static bool Internal => true;

    /// <summary>The saved grants; one per path — granting a path again replaces it.</summary>
    [Out, Store] public global::app.type.item.list.@this<global::app.type.item.permission.@this> Grant { get; set; } = new();
}
