namespace app.module.action.identity.setting;

/// <summary>
/// The identities an actor holds — <c>%!identity%</c>. An actor's own: a user with none reads none, never
/// the system's (it would sign as the system). The app's identities are the system actor's.
/// </summary>
[global::app.type.item.setting.Own]
public sealed class @this : global::app.type.item.setting.module.@this
{
    /// <summary>Each identity, its keys with it; the one marked default signs for the actor.</summary>
    [Out, Store] public global::app.type.item.list.@this<global::app.module.action.identity.Identity> Identity { get; set; } = new();
}
