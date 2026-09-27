namespace app.type.item.setting;

/// <summary>
/// On a setting class: an actor's own. The actor's settings read only its own row and its own run's values —
/// never those of the actor it falls back to (the user's never the system's). Identity and permission are:
/// a user with no identity must not sign as the system, one with no grant must not hold the system's.
/// </summary>
[System.AttributeUsage(System.AttributeTargets.Class, Inherited = false)]
public sealed class OwnAttribute : System.Attribute;
